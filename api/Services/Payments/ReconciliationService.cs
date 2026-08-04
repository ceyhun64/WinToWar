using System.Data;
using api.Models;
using api.Models.Payments;
using api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services.Payments;

/// <summary>
/// Bölüm 0.2 adım 9: reconciliation job. Distributed lock (DB-tablosu tabanlı,
/// bkz. ReconciliationLock) altında periyodik olarak: bekleyen payout/refund
/// gönderimlerini + retry'larını işler, gönderilmiş olanlar için gerçekleşen
/// (actual) fee'yi sorgulayıp Completed'a taşır.
///
/// PeriodicTimer + CancellationToken kullanılır (Bölüm 0.8/0.11 — asılı task/
/// kaynak sızıntısı olmasın). BackgroundService singleton olduğundan her tick'te
/// yeni bir DI scope açılır (PaymentDbContext scoped'dır).
/// </summary>
public class ReconciliationService : BackgroundService
{
    private const string LockName = "reconciliation";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PaymentConfig _config;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReconciliationService> _logger;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    public ReconciliationService(
        IServiceScopeFactory scopeFactory,
        IOptions<PaymentConfig> config,
        TimeProvider timeProvider,
        ILogger<ReconciliationService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_config.ReconciliationIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunTickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reconciliation tick sırasında beklenmeyen hata.");
            }
        }
    }

    private async Task RunTickAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        if (!await TryAcquireLockAsync(db, cancellationToken))
        {
            return; // başka bir instance kilidi tutuyor.
        }

        try
        {
            var payoutService = scope.ServiceProvider.GetRequiredService<PayoutService>();
            var refundService = scope.ServiceProvider.GetRequiredService<RefundService>();

            await payoutService.ProcessDuePayoutsAsync(cancellationToken);
            await payoutService.ReconcileSentPayoutsAsync(cancellationToken);
            await refundService.ProcessDueRefundsAsync(cancellationToken);
            await ReconcileSentRefundsAndCloseInvoicesAsync(scope, cancellationToken);
        }
        finally
        {
            await ReleaseLockAsync(db, cancellationToken);
        }
    }

    private async Task<bool> TryAcquireLockAsync(PaymentDbContext db, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var existing = await db.ReconciliationLocks.FirstOrDefaultAsync(l => l.LockName == LockName, cancellationToken);
        if (existing is null)
        {
            db.ReconciliationLocks.Add(new ReconciliationLock
            {
                LockName = LockName,
                HolderId = _instanceId,
                ExpiresAt = now.AddSeconds(_config.ReconciliationLockTimeoutSeconds)
            });
        }
        else if (existing.ExpiresAt <= now)
        {
            existing.HolderId = _instanceId;
            existing.ExpiresAt = now.AddSeconds(_config.ReconciliationLockTimeoutSeconds);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false; // eşzamanlı başka bir instance kilidi kazandı.
        }
    }

    private async Task ReleaseLockAsync(PaymentDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.ReconciliationLocks
            .FirstOrDefaultAsync(l => l.LockName == LockName && l.HolderId == _instanceId, cancellationToken);
        if (existing is not null)
        {
            existing.ExpiresAt = _timeProvider.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// RefundService.ReconcileSentRefundsAsync'i çağırır; tamamlanan her refund
    /// için ilgili PaymentInvoice'ı Refunded'a taşır (StatusRank guard'ıyla).
    /// </summary>
    private async Task ReconcileSentRefundsAndCloseInvoicesAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var refundService = scope.ServiceProvider.GetRequiredService<RefundService>();

        var beforeCompletedIds = await db.Refunds
            .Where(r => r.Status == RefundStatus.Completed)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        await refundService.ReconcileSentRefundsAsync(cancellationToken);

        var newlyCompleted = await db.Refunds
            .Where(r => r.Status == RefundStatus.Completed && !beforeCompletedIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        foreach (var refund in newlyCompleted)
        {
            var invoice = await db.PaymentInvoices.FirstOrDefaultAsync(i => i.Id == refund.PaymentInvoiceId, cancellationToken);
            if (invoice is not null && StatusRankPolicy.IsForwardTransition(invoice.Status, PaymentInvoiceStatus.Refunded))
            {
                invoice.Status = PaymentInvoiceStatus.Refunded;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    // 🛠️ DÜZELTME (bu turda kaldırıldı — gerçek bir para/kural hatasıydı): burada
    // önceden bir "ProcessMatchmakingTimeoutsAsync" güvence ağı vardı; gerekçesi
    // "EconomyTickService zaten lobi zaman aşımında maçı otomatik iptal edip
    // refund tetikliyor, bu yalnızca kaçırılan tetiklemeleri yakalıyor" idi — bu
    // öncül YANLIŞTI. EconomyTickService.TickLobbyAsync yalnızca tek seferlik bir
    // "LobbyTimeoutReached" bildirimi yayınlar (docs/03-game-rules.md Bölüm 7 /
    // docs/05-payment.md Bölüm 1.6: "Otomatik iade YOKTUR", karar oyuncuya aittir
    // — İptal Et/Beklemeye Devam Et). Bu metot, süre dolan HER onaylı invoice'ı
    // oyuncunun tercihine bakmaksızın koşulsuz refund ediyordu — "Beklemeye Devam
    // Et" seçen bir oyuncunun parası onun haberi/isteği olmadan iade ediliyor,
    // ama oyuncu lobide rezerve kalmaya devam ediyordu (RemovePlayerFromLobby hiç
    // çağrılmıyordu) — bu hem 🔒 "otomatik iade yok" kuralını ihlal ediyor hem de
    // "parası iade edilmiş ama hâlâ lobide/maçta" tutarsız bir durum yaratıyordu.
    // Ayrıca metodun kendi gerekçesi olan "süreç yeniden başlatma sonrası kaçırılan
    // tetikleme" senaryosunda da işe yaramıyordu: MatchManager bellek içi olduğundan
    // bir restart'tan sonra ilgili Match zaten hiç bulunamıyor (TryGetMatch false
    // döner), yani tam da "kurtarmak" istediği durumda sessizce hiçbir şey yapmıyordu.
}
