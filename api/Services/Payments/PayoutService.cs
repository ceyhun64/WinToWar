using System.Globalization;
using api.Models.Payments;
using api.Models.Payments.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services.Payments;

/// <summary>
/// Bölüm 3.2 akışı: maç bittiğinde (EconomyTickService hook'u üzerinden) çağrılır.
/// Kazanç artık on-chain LTC olarak gönderilmez — doğrudan kazananların
/// Wallet.BalanceUsd'sine kredi olarak işlenir (2026-08-08 kararı: LTC adresi
/// yalnızca WithdrawalRequest üzerinden, oyuncu kendi isteğiyle para çekerken
/// istenir — bkz. docs/05-payment.md Bölüm 1.9). Bu yüzden ayrı bir gönderim/retry/
/// reconciliation adımına ihtiyaç yoktur: <see cref="ProcessPayoutAsync"/> havuzu
/// hesaplar, Payout + her kazanan için bir PayoutRecipient satırını INSERT eder ve
/// AYNI transaction içinde kazananların bakiyesine kredi uygular — hepsi tek
/// atomik adımdır (Payout.MatchId unique index'i idempotency garantisidir).
/// </summary>
public class PayoutService
{
    private readonly PaymentDbContext _db;
    private readonly WalletService _walletService;
    private readonly Services.MatchManager _matchManager;
    private readonly PaymentConfig _config;
    private readonly TimeProvider _timeProvider;
    private readonly PaymentEventNotifier _notifier;
    private readonly ILogger<PayoutService> _logger;

    public PayoutService(
        PaymentDbContext db,
        WalletService walletService,
        Services.MatchManager matchManager,
        IOptions<PaymentConfig> config,
        TimeProvider timeProvider,
        PaymentEventNotifier notifier,
        ILogger<PayoutService> logger)
    {
        _db = db;
        _walletService = walletService;
        _matchManager = matchManager;
        _config = config.Value;
        _timeProvider = timeProvider;
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>
    /// docs/05-payment.md Bölüm 1.1: TotalPoolUsd = Room.EntryFeeUsd × fiilen
    /// ödemesi onaylanmış oyuncu sayısı. Bir oyuncu odaya PaymentInvoice hiç
    /// oluşturmadan (mevcut Wallet bakiyesinden doğrudan, bkz. RoomEntryService)
    /// katılmış olabilir — invoice toplamına güvenmek bu oyuncunun katkısını
    /// sessizce havuzdan düşürürdü, bu yüzden havuz doğrudan Room ayarlarından ve
    /// onaylı oyuncu sayısından hesaplanır.
    /// 🛠️ docs/03-game-rules.md Bölüm 7 (bot politikası): botlar IsPaymentConfirmed=true
    /// olsa da (maçın başlaması için gerekli, bkz. MatchManager) hiçbir zaman gerçek
    /// para yatırmaz — havuz hesabından (!IsBot) ile hariç tutulur.
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

        var confirmedPlayerCount = match.Players.Count(p => p.IsPaymentConfirmed && !p.IsBot);
        var totalPoolUsd = match.Room.EntryFeeUsd * confirmedPlayerCount;
        if (totalPoolUsd <= 0)
        {
            _logger.LogInformation("Havuz 0 (ücretsiz oda), payout oluşturulmuyor: {MatchId}", matchId);
            return;
        }

        // Ara adımlarda Round çağrılmaz (Bölüm 2.3) — yalnızca kalıcılaştırma anında, tek seferde yuvarlanır.
        var commissionUsd = totalPoolUsd * _config.CommissionRate;
        var winnerCount = winnerPlayerIds.Count;
        var grossPerWinnerUsd = (totalPoolUsd - commissionUsd) / winnerCount;

        var now = _timeProvider.GetUtcNow();
        var payout = new Payout
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            TotalPoolUsd = PaymentMath.RoundUsdForPersistence(totalPoolUsd),
            CommissionUsd = PaymentMath.RoundUsdForPersistence(commissionUsd),
            WinnerCount = winnerCount,
            CreatedAt = now
        };

        var recipients = winnerPlayerIds.Select(winnerPlayerId => new PayoutRecipient
        {
            Id = Guid.NewGuid(),
            PayoutId = payout.Id,
            WinnerPlayerId = winnerPlayerId,
            AmountUsd = PaymentMath.RoundUsdForPersistence(grossPerWinnerUsd),
            CreatedAt = now
        }).ToList();

        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        _db.Payouts.Add(payout);
        _db.PayoutRecipients.AddRange(recipients);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);

            foreach (var recipient in recipients)
            {
                await _walletService.CreditAsync(recipient.WinnerPlayerId, recipient.AmountUsd, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Eşzamanlı iki maç-bitiş tetiklemesi (ör. tick + manuel çağrı) — unique-violation-as-no-op.
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogInformation(ex, "Payout INSERT çakışması, zaten oluşturulmuş: {MatchId}", matchId);
            return;
        }

        _logger.LogInformation("Payout tamamlandı: {MatchId}, kazanan sayısı={WinnerCount}", matchId, winnerCount);

        // docs/16-wallet-balance-sync.md Bölüm 1 "Önemli": transaction commit
        // olduktan SONRA, her kazanan için ayrı bildirim (her birinin bakiyesi
        // farklı, tek bir grup mesajı olmaz).
        foreach (var recipient in recipients)
        {
            await _walletService.NotifyBalanceChangedAsync(recipient.WinnerPlayerId, cancellationToken);
        }

        await _notifier.NotifyPayoutCompletedAsync(matchId, new PayoutCompletedEvent
        {
            MatchId = matchId,
            Recipients = recipients.Select(ToRecipientDto).ToList()
        }, cancellationToken);
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
            TotalPoolUsd = payout.TotalPoolUsd.ToString("0.00", CultureInfo.InvariantCulture),
            CommissionUsd = payout.CommissionUsd.ToString("0.00", CultureInfo.InvariantCulture),
            Recipients = recipients.Select(ToRecipientDto).ToList()
        };
    }

    private static PayoutRecipientDto ToRecipientDto(PayoutRecipient r) => new()
    {
        WinnerPlayerId = r.WinnerPlayerId,
        AmountUsd = r.AmountUsd.ToString("0.00", CultureInfo.InvariantCulture)
    };
}
