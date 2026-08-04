using System.Globalization;
using api.Models.Payments;
using api.Models.Payments.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services.Payments;

/// <summary>
/// Bölüm 3.2 akışı + Bölüm 5.2 state machine'i + retry/backoff/jitter uygular.
/// v8: <see cref="Payout"/> maç başına bir agregatördür, <see cref="PayoutRecipient"/>
/// her kazanan için ayrı bir satırdır (1-N, N=1 normal senaryoda) — bkz. Bölüm 2.2.
/// <see cref="ProcessPayoutAsync"/> maç bittiğinde (EconomyTickService hook'u
/// üzerinden) çağrılır: havuzu hesaplar, Payout(PayoutPending) + her kazanan için
/// bir PayoutRecipient(PayoutPending) satırını kalıcılaştırır (idempotent —
/// Payout.MatchId unique). Asıl BTCPay gönderimi ve retry döngüsü
/// <see cref="ProcessDuePayoutsAsync"/> ile ReconciliationService'in periyodik
/// tick'inden ayrıca yürütülür (nested-transaction sorunlarından kaçınmak için).
/// </summary>
public class PayoutService
{
    private readonly PaymentDbContext _db;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IPriceOracle _priceOracle;
    private readonly WalletService _walletService;
    private readonly Services.MatchManager _matchManager;
    private readonly PaymentConfig _config;
    private readonly TimeProvider _timeProvider;
    private readonly PaymentEventNotifier _notifier;
    private readonly ILogger<PayoutService> _logger;

    public PayoutService(
        PaymentDbContext db,
        IPaymentProvider paymentProvider,
        IPriceOracle priceOracle,
        WalletService walletService,
        Services.MatchManager matchManager,
        IOptions<PaymentConfig> config,
        TimeProvider timeProvider,
        PaymentEventNotifier notifier,
        ILogger<PayoutService> logger)
    {
        _db = db;
        _paymentProvider = paymentProvider;
        _priceOracle = priceOracle;
        _walletService = walletService;
        _matchManager = matchManager;
        _config = config.Value;
        _timeProvider = timeProvider;
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>
    /// docs/05-payment.md Bölüm 1.1: TotalPoolUsd = Room.EntryFeeUsd × Room.MaxPlayers.
    /// 🛠️ v9 düzeltmesi: havuz artık PaymentInvoice toplamından değil, doğrudan
    /// Room ayarlarından ve fiilen ödemesi onaylanmış oyuncu sayısından hesaplanır.
    /// Bir oyuncu odaya PaymentInvoice hiç oluşturmadan (mevcut Wallet bakiyesinden
    /// doğrudan, bkz. RoomEntryService) katılmış olabilir — invoice toplamına
    /// güvenmek bu oyuncunun katkısını sessizce havuzdan düşürürdü. Kazananın ödül
    /// adresi de aynı nedenle önce invoice'tan, yoksa Wallet'tan (bkz.
    /// WalletService.ResolveAndSavePayoutAddressAsync) çözülür.
    /// </summary>
    public async Task ProcessPayoutAsync(string matchId, IReadOnlyList<string> winnerPlayerIds, CancellationToken cancellationToken)
    {
        if (winnerPlayerIds.Count == 0)
        {
            return;
        }

        var alreadyExists = await _db.Payouts.AnyAsync(p => p.MatchId == matchId, cancellationToken);
        if (alreadyExists)
        {
            return; // Bölüm 3.2: "Payout(MatchId=X) var mı? -> no-op" (agregatör seviyesinde idempotency).
        }

        if (!_matchManager.TryGetMatch(matchId, out var match))
        {
            _logger.LogError("Payout tetiklendi ama maç artık bulunamıyor: {MatchId}", matchId);
            return;
        }

        var confirmedPlayerCount = match.Players.Count(p => p.IsPaymentConfirmed);
        var totalPoolUsd = match.Room.EntryFeeUsd * confirmedPlayerCount;
        if (totalPoolUsd <= 0)
        {
            _logger.LogInformation("Havuz 0 (ücretsiz oda), payout oluşturulmuyor: {MatchId}", matchId);
            return;
        }

        var quote = await _priceOracle.GetRateAsync(cancellationToken);
        // Ara adımlarda Round çağrılmaz (Bölüm 2.3) — TotalPoolLtc yuvarlanmamış ara değerdir.
        var totalPoolLtc = totalPoolUsd / quote.UsdPerLtc;
        var commissionLtc = PaymentMath.CalculateCommission(totalPoolLtc, _config.CommissionRate);
        var winnerCount = winnerPlayerIds.Count;

        var confirmedInvoiceByPlayer = (await _db.PaymentInvoices
                .Where(i => i.MatchId == matchId && i.Status == PaymentInvoiceStatus.Confirmed)
                .ToListAsync(cancellationToken))
            .GroupBy(i => i.PlayerId)
            .ToDictionary(g => g.Key, g => g.First());

        var payout = new Payout
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            TotalPoolLtc = PaymentMath.RoundForPersistence(totalPoolLtc),
            CommissionLtc = PaymentMath.RoundForPersistence(commissionLtc),
            WinnerCount = winnerCount,
            Status = PayoutStatus.PayoutPending,
            CreatedAt = _timeProvider.GetUtcNow()
        };

        var recipients = new List<PayoutRecipient>();
        foreach (var winnerPlayerId in winnerPlayerIds)
        {
            var payoutAddress = confirmedInvoiceByPlayer.TryGetValue(winnerPlayerId, out var winnerInvoice)
                ? winnerInvoice.PayoutAddress
                : await _walletService.GetPayoutAddressAsync(winnerPlayerId, cancellationToken);

            if (payoutAddress is null)
            {
                _logger.LogError(
                    "Kazananın hiçbir ödül adresi (invoice ya da Wallet'ta kayıtlı) bulunamadı, payout satırı atlanıyor: {MatchId}, {PlayerId}",
                    matchId, winnerPlayerId);
                continue;
            }

            // estimatedFee yalnızca bu kazanana ne kadar net gönderileceğini hesaplamak
            // için geçici bir girdidir (Bölüm 2.6); DB'ye asla bu tahminle yazılmaz.
            var estimatedFeeLtc = EstimateNetworkFeeLtc();
            var amountLtc = PaymentMath.CalculatePerWinnerShareLtc(totalPoolLtc, commissionLtc, winnerCount, estimatedFeeLtc);

            recipients.Add(new PayoutRecipient
            {
                Id = Guid.NewGuid(),
                PayoutId = payout.Id,
                WinnerPlayerId = winnerPlayerId,
                PayoutAddress = payoutAddress,
                AmountLtc = PaymentMath.RoundForPersistence(amountLtc),
                NetworkFeeLtc = null, // 🔒 Bölüm 2.6: yalnızca actual fee ile, reconciliation'da doldurulur.
                Status = PayoutStatus.PayoutPending
            });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        _db.Payouts.Add(payout);
        _db.PayoutRecipients.AddRange(recipients);
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

        _logger.LogInformation("Payout kaydı oluşturuldu: {MatchId}, kazanan sayısı={WinnerCount}", matchId, winnerCount);
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
        var candidates = await _db.PayoutRecipients
            .Where(r => r.Status == PayoutStatus.PayoutPending && r.BtcPayTransactionId == null)
            .ToListAsync(cancellationToken);
        var due = candidates.Where(r => r.NextRetryAt is null || r.NextRetryAt <= now).ToList();

        foreach (var recipient in due)
        {
            await TrySendAsync(recipient, cancellationToken);
        }
    }

    private async Task TrySendAsync(PayoutRecipient recipient, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _paymentProvider.SendPayoutAsync(recipient.PayoutAddress, recipient.AmountLtc, cancellationToken);
            recipient.BtcPayTransactionId = result.BtcPayTransactionId;
            recipient.Status = PayoutStatus.PayoutSent;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Payout gönderildi: {RecipientId} -> {TxId}", recipient.Id, result.BtcPayTransactionId);
        }
        catch (Exception ex)
        {
            await HandleSendFailureAsync(recipient, ex, cancellationToken);
        }
    }

    private async Task HandleSendFailureAsync(PayoutRecipient recipient, Exception ex, CancellationToken cancellationToken)
    {
        recipient.RetryCount += 1;
        var now = _timeProvider.GetUtcNow();

        if (recipient.RetryCount > _config.PayoutRetryCount)
        {
            recipient.Status = PayoutStatus.Failed;
            recipient.NextRetryAt = null;
            _logger.LogError(ex, "Payout kalıcı olarak başarısız (retry limiti aşıldı): {RecipientId}", recipient.Id);
        }
        else
        {
            var jitterSeconds = Random.Shared.Next(0, Math.Max(1, _config.PayoutRetryJitterSeconds));
            var backoffSeconds = _config.PayoutRetryBaseDelaySeconds * Math.Pow(_config.RetryBackoffMultiplier, recipient.RetryCount - 1);
            recipient.NextRetryAt = now.AddSeconds(backoffSeconds + jitterSeconds);
            _logger.LogWarning(ex, "Payout gönderimi başarısız, {RetryAt} zamanında tekrar denenecek: {RecipientId} (deneme {RetryCount}/{MaxRetries})",
                recipient.NextRetryAt, recipient.Id, recipient.RetryCount, _config.PayoutRetryCount);
        }

        if (recipient.Status == PayoutStatus.Failed)
        {
            await RecalculateAggregateStatusAsync(recipient.PayoutId, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>docs/07-pages.md `/mac/[matchId]`: bir maçın ödül özetini okur, henüz payout yoksa null döner.</summary>
    public async Task<PayoutSummaryDto?> GetPayoutSummaryAsync(string matchId, CancellationToken cancellationToken)
    {
        var payout = await _db.Payouts.AsNoTracking().FirstOrDefaultAsync(p => p.MatchId == matchId, cancellationToken);
        if (payout is null)
        {
            return null;
        }

        var recipients = await _db.PayoutRecipients.AsNoTracking()
            .Where(r => r.PayoutId == payout.Id)
            .ToListAsync(cancellationToken);

        return new PayoutSummaryDto
        {
            MatchId = payout.MatchId,
            Status = payout.Status.ToString(),
            TotalPoolLtc = payout.TotalPoolLtc.ToString("0.00000000", CultureInfo.InvariantCulture),
            CommissionLtc = payout.CommissionLtc.ToString("0.00000000", CultureInfo.InvariantCulture),
            Recipients = recipients.Select(r => new PayoutRecipientDto
            {
                WinnerPlayerId = r.WinnerPlayerId,
                PayoutAddress = r.PayoutAddress,
                AmountLtc = r.AmountLtc.ToString("0.00000000", CultureInfo.InvariantCulture),
                Status = r.Status.ToString(),
                BtcPayTransactionId = r.BtcPayTransactionId
            }).ToList()
        };
    }

    /// <summary>
    /// ReconciliationService tarafından çağrılır: PayoutSent durumundaki kayıtlar
    /// için gerçekleşen (actual) fee'yi sorgular, yalnızca null olan NetworkFeeLtc
    /// alanını bir kez doldurur ve Completed'a taşır (Bölüm 2.6, 10 — kendi
    /// başına idempotency garantisi: zaten dolu bir kayda tekrar yazılmaz). N&gt;1
    /// senaryoda her PayoutRecipient ayrı ayrı taranır, biri dolduruldu diye
    /// diğerleri atlanmaz.
    /// </summary>
    public async Task ReconcileSentPayoutsAsync(CancellationToken cancellationToken)
    {
        var sent = await _db.PayoutRecipients.Where(r => r.Status == PayoutStatus.PayoutSent && r.NetworkFeeLtc == null).ToListAsync(cancellationToken);
        foreach (var recipient in sent)
        {
            var actualFee = await _paymentProvider.GetActualNetworkFeeAsync(recipient.BtcPayTransactionId!, cancellationToken);
            if (actualFee is null)
            {
                continue; // henüz on-chain doğrulanmadı, sonraki tick'te tekrar denenir.
            }

            recipient.NetworkFeeLtc = PaymentMath.RoundForPersistence(actualFee.Value);
            recipient.Status = PayoutStatus.Completed;
            recipient.CompletedAt = _timeProvider.GetUtcNow();
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Kazanana payout tamamlandı: {RecipientId}, actualFee={ActualFee}", recipient.Id, recipient.NetworkFeeLtc);

            await RecalculateAggregateStatusAsync(recipient.PayoutId, cancellationToken);
        }
    }

    /// <summary>
    /// Bölüm 5.2: Payout.Status, kendi PayoutRecipient çocuklarının durumundan
    /// türetilir — birden fazla yerde elle tekrar hesaplanmaz, tek bu metottan.
    /// Tüm çocuklar Completed olduğunda SignalR "PayoutCompleted" (v8 PayoutDto,
    /// Recipients dizisiyle) yayınlanır.
    /// </summary>
    private async Task RecalculateAggregateStatusAsync(Guid payoutId, CancellationToken cancellationToken)
    {
        var payout = await _db.Payouts.FirstOrDefaultAsync(p => p.Id == payoutId, cancellationToken);
        if (payout is null)
        {
            return;
        }

        var recipients = await _db.PayoutRecipients.Where(r => r.PayoutId == payoutId).ToListAsync(cancellationToken);
        var previousStatus = payout.Status;

        if (recipients.All(r => r.Status == PayoutStatus.Completed))
        {
            payout.Status = PayoutStatus.Completed;
            payout.CompletedAt = _timeProvider.GetUtcNow();
        }
        else if (recipients.Any(r => r.Status == PayoutStatus.Failed))
        {
            payout.Status = PayoutStatus.Failed;
        }
        else
        {
            payout.Status = PayoutStatus.PayoutPending;
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (payout.Status == PayoutStatus.Completed && previousStatus != PayoutStatus.Completed)
        {
            await _notifier.NotifyPayoutCompletedAsync(payout.MatchId, new PayoutCompletedEvent
            {
                MatchId = payout.MatchId,
                Recipients = recipients.Select(r => new PayoutRecipientDto
                {
                    WinnerPlayerId = r.WinnerPlayerId,
                    PayoutAddress = r.PayoutAddress,
                    AmountLtc = r.AmountLtc.ToString("0.00000000", CultureInfo.InvariantCulture),
                    Status = r.Status.ToString(),
                    BtcPayTransactionId = r.BtcPayTransactionId
                }).ToList()
            }, cancellationToken);
        }
    }
}
