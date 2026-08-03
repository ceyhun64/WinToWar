using System.Globalization;
using api.Models.Payments;
using api.Models.Payments.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services.Payments;

/// <summary>
/// Bölüm 3.2 akışı + Bölüm 5.2 state machine'i + retry/backoff/jitter uygular.
/// <see cref="ProcessPayoutAsync"/> maç bittiğinde (EconomyTickService hook'u
/// üzerinden) çağrılır: havuzu hesaplar, Payout(PayoutPending) satırını
/// kalıcılaştırır (idempotent — MatchId unique). Asıl BTCPay gönderimi ve
/// retry döngüsü <see cref="ProcessDuePayoutsAsync"/> ile ReconciliationService'in
/// periyodik tick'inden ayrıca yürütülür (nested-transaction sorunlarından kaçınmak için).
/// </summary>
public class PayoutService
{
    private readonly PaymentDbContext _db;
    private readonly IPaymentProvider _paymentProvider;
    private readonly PaymentConfig _config;
    private readonly TimeProvider _timeProvider;
    private readonly PaymentEventNotifier _notifier;
    private readonly ILogger<PayoutService> _logger;

    public PayoutService(
        PaymentDbContext db,
        IPaymentProvider paymentProvider,
        IOptions<PaymentConfig> config,
        TimeProvider timeProvider,
        PaymentEventNotifier notifier,
        ILogger<PayoutService> logger)
    {
        _db = db;
        _paymentProvider = paymentProvider;
        _config = config.Value;
        _timeProvider = timeProvider;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task ProcessPayoutAsync(string matchId, string winnerPlayerId, CancellationToken cancellationToken)
    {
        var alreadyExists = await _db.Payouts.AnyAsync(p => p.MatchId == matchId, cancellationToken);
        if (alreadyExists)
        {
            return; // Bölüm 3.2: "Payout(MatchId=X) var mı? -> no-op" (idempotency).
        }

        var confirmedInvoices = await _db.PaymentInvoices
            .Where(i => i.MatchId == matchId && i.Status == PaymentInvoiceStatus.Confirmed)
            .ToListAsync(cancellationToken);

        if (confirmedInvoices.Count == 0)
        {
            _logger.LogWarning("Maç bitti ama onaylanmış hiçbir invoice yok, payout oluşturulmuyor: {MatchId}", matchId);
            return;
        }

        // Ara adımlarda Round çağrılmaz (Bölüm 2.3) — TotalPoolLtc yuvarlanmamış ara değerdir.
        var totalPoolLtc = confirmedInvoices.Sum(i => i.AmountLtc);
        var commissionLtc = PaymentMath.CalculateCommission(totalPoolLtc, _config.CommissionRate);

        // estimatedFee yalnızca kazanana ne kadar net gönderileceğini hesaplamak için
        // geçici bir girdidir (Bölüm 2.6); DB'ye asla bu tahminle yazılmaz.
        var estimatedFeeLtc = EstimateNetworkFeeLtc();
        var amountLtc = PaymentMath.CalculatePayoutAmount(totalPoolLtc, commissionLtc, estimatedFeeLtc);

        var payout = new Payout
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            WinnerPlayerId = winnerPlayerId,
            TotalPoolLtc = PaymentMath.RoundForPersistence(totalPoolLtc),
            CommissionLtc = PaymentMath.RoundForPersistence(commissionLtc),
            NetworkFeeLtc = null, // 🔒 Bölüm 2.6: yalnızca actual fee ile, reconciliation'da doldurulur.
            AmountLtc = PaymentMath.RoundForPersistence(amountLtc),
            Status = PayoutStatus.PayoutPending,
            CreatedAt = _timeProvider.GetUtcNow()
        };

        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        _db.Payouts.Add(payout);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Eşzamanlı iki maç-bitiş tetiklemesi (ör. tick + manuel çağrı) — unique-violation-as-no-op.
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogInformation(ex, "Payout INSERT çakışması, zaten oluşturulmuş: {MatchId}", matchId);
            return;
        }

        _logger.LogInformation("Payout kaydı oluşturuldu: {MatchId}, kazanan={WinnerId}, tutar={AmountLtc} LTC", matchId, winnerPlayerId, payout.AmountLtc);
    }

    /// <summary>
    /// 🛠️ Varsayım: gerçek bir cüzdan olmadan tahmini fee hesaplanamaz; sabit,
    /// gerçekçi bir tahmini değer kullanılır (FakePaymentProvider ile aynı
    /// büyüklük mertebesinde). Gerçek BTCPay entegrasyonunda bu, BTCPay'in fee
    /// tahmin uç noktasından alınacaktır.
    /// </summary>
    private static decimal EstimateNetworkFeeLtc() => 0.00050000m;

    /// <summary>ReconciliationService tarafından periyodik çağrılır.</summary>
    public async Task ProcessDuePayoutsAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        // 🛠️ Not: NextRetryAt (DateTimeOffset) karşılaştırması client-side yapılır —
        // SQLite EF Core provider'ı bu tür ifadeleri WHERE içinde translate edemiyor.
        var candidates = await _db.Payouts
            .Where(p => p.Status == PayoutStatus.PayoutPending && p.BtcPayTransactionId == null)
            .ToListAsync(cancellationToken);
        var due = candidates.Where(p => p.NextRetryAt is null || p.NextRetryAt <= now).ToList();

        foreach (var payout in due)
        {
            await TrySendAsync(payout, cancellationToken);
        }
    }

    private async Task TrySendAsync(Payout payout, CancellationToken cancellationToken)
    {
        var winnerInvoice = await _db.PaymentInvoices.AsNoTracking()
            .Where(i => i.MatchId == payout.MatchId && i.PlayerId == payout.WinnerPlayerId && i.Status == PaymentInvoiceStatus.Confirmed)
            .FirstOrDefaultAsync(cancellationToken);

        if (winnerInvoice is null)
        {
            _logger.LogError("Payout için kazananın onaylanmış invoice'ı bulunamadı: {PayoutId}", payout.Id);
            return;
        }

        try
        {
            var result = await _paymentProvider.SendPayoutAsync(winnerInvoice.PayoutAddress, payout.AmountLtc, cancellationToken);
            payout.BtcPayTransactionId = result.BtcPayTransactionId;
            payout.Status = PayoutStatus.PayoutSent;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Payout gönderildi: {PayoutId} -> {TxId}", payout.Id, result.BtcPayTransactionId);
        }
        catch (Exception ex)
        {
            await HandleSendFailureAsync(payout, ex, cancellationToken);
        }
    }

    private async Task HandleSendFailureAsync(Payout payout, Exception ex, CancellationToken cancellationToken)
    {
        payout.RetryCount += 1;
        var now = _timeProvider.GetUtcNow();

        if (payout.RetryCount > _config.PayoutRetryCount)
        {
            payout.Status = PayoutStatus.Failed;
            payout.NextRetryAt = null;
            _logger.LogError(ex, "Payout kalıcı olarak başarısız (retry limiti aşıldı): {PayoutId}", payout.Id);
        }
        else
        {
            var jitterSeconds = Random.Shared.Next(0, Math.Max(1, _config.PayoutRetryJitterSeconds));
            var backoffSeconds = _config.PayoutRetryBaseDelaySeconds * Math.Pow(2, payout.RetryCount - 1);
            payout.NextRetryAt = now.AddSeconds(backoffSeconds + jitterSeconds);
            _logger.LogWarning(ex, "Payout gönderimi başarısız, {RetryAt} zamanında tekrar denenecek: {PayoutId} (deneme {RetryCount}/{MaxRetries})",
                payout.NextRetryAt, payout.Id, payout.RetryCount, _config.PayoutRetryCount);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// ReconciliationService tarafından çağrılır: PayoutSent durumundaki kayıtlar
    /// için gerçekleşen (actual) fee'yi sorgular, yalnızca null olan NetworkFeeLtc
    /// alanını bir kez doldurur ve Completed'a taşır (Bölüm 2.6, 10 — kendi
    /// başına idempotency garantisi: zaten dolu bir kayda tekrar yazılmaz).
    /// </summary>
    public async Task ReconcileSentPayoutsAsync(CancellationToken cancellationToken)
    {
        var sent = await _db.Payouts.Where(p => p.Status == PayoutStatus.PayoutSent && p.NetworkFeeLtc == null).ToListAsync(cancellationToken);
        foreach (var payout in sent)
        {
            var actualFee = await _paymentProvider.GetActualNetworkFeeAsync(payout.BtcPayTransactionId!, cancellationToken);
            if (actualFee is null)
            {
                continue; // henüz on-chain doğrulanmadı, sonraki tick'te tekrar denenir.
            }

            payout.NetworkFeeLtc = PaymentMath.RoundForPersistence(actualFee.Value);
            payout.Status = PayoutStatus.Completed;
            payout.CompletedAt = _timeProvider.GetUtcNow();
            await _db.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyPayoutCompletedAsync(payout.MatchId, new PayoutCompletedEvent
            {
                MatchId = payout.MatchId,
                WinnerPlayerId = payout.WinnerPlayerId,
                AmountLtc = payout.AmountLtc.ToString("0.00000000", CultureInfo.InvariantCulture)
            }, cancellationToken);

            _logger.LogInformation("Payout tamamlandı: {PayoutId}, actualFee={ActualFee}", payout.Id, payout.NetworkFeeLtc);
        }
    }
}
