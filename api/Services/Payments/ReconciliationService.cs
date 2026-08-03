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
/// (actual) fee'yi sorgulayıp Completed'a taşır, ve Bölüm 1.6'daki eşleşme
/// bulunamama zaman aşımı refund'unu tetikler.
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
            await ProcessMatchmakingTimeoutsAsync(scope, cancellationToken);
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

    /// <summary>
    /// Bölüm 1.6: MatchmakingTimeoutSeconds içinde rakip bulunamazsa tam otomatik
    /// refund. Oyun motoru state'i bellekte tutulduğundan (MatchManager) burada
    /// yalnızca okuma amaçlı erişilir — hiçbir oyun state mutasyonu yapılmaz.
    /// </summary>
    private async Task ProcessMatchmakingTimeoutsAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var matchManager = scope.ServiceProvider.GetRequiredService<MatchManager>();
        var refundService = scope.ServiceProvider.GetRequiredService<RefundService>();
        var now = _timeProvider.GetUtcNow();

        var scanWindowStart = now.AddMinutes(-_config.ReconciliationScanWindowMinutes);

        // 🛠️ Not: ConfirmedAt (DateTimeOffset) karşılaştırması client-side yapılır —
        // SQLite EF Core provider'ı bu tür ifadeleri WHERE içinde translate edemiyor.
        var confirmedInvoices = await db.PaymentInvoices
            .Where(i => i.Status == PaymentInvoiceStatus.Confirmed && i.ConfirmedAt != null)
            .ToListAsync(cancellationToken);
        var candidates = confirmedInvoices.Where(i => i.ConfirmedAt >= scanWindowStart).ToList();

        foreach (var invoice in candidates)
        {
            if (!matchManager.TryGetMatch(invoice.MatchId, out var match))
            {
                continue;
            }

            bool stillWaitingForOpponent;
            lock (match.Lock)
            {
                stillWaitingForOpponent = match.Status == MatchStatus.WaitingForPlayers;
            }

            if (!stillWaitingForOpponent)
            {
                continue;
            }

            var elapsedSinceConfirmed = (now - invoice.ConfirmedAt!.Value).TotalSeconds;
            if (elapsedSinceConfirmed < _config.MatchmakingTimeoutSeconds)
            {
                continue;
            }

            await refundService.SubmitAsync(invoice, invoice.AmountLtc, RefundReason.MatchmakingTimeout, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Eşleşme bulunamadı, refund kuyruğa alındı: invoice={InvoiceId}, maç={MatchId}", invoice.Id, invoice.MatchId);
        }
    }
}
