using api;
using api.Models.Payments;
using api.Services;
using api.Services.Payments;
using api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api.Tests;

/// <summary>
/// Bölüm 3.2 + 5.2 + 2.2 (v8) + 2.6 + 1.1 (v9): payout hesaplama, gönderim, ve yalnızca
/// actual fee'nin kalıcılaşması (reconciliation) uçtan uca — gerçek SQLite'a karşı.
/// v8: Payout maç-bazlı agregatör, PayoutRecipient kazanan-bazlı (1-N). v9: havuz artık
/// Room.EntryFeeUsd × onaylı oyuncu sayısından (bkz. MatchFactory), sabit kur 40 USD/LTC
/// seçildi ki beklenen değerler yuvarlama belirsizliği olmadan hesaplanabilsin.
/// </summary>
public class PayoutServiceIntegrationTests : IDisposable
{
    private const decimal FixedRateUsdPerLtc = 40.0m;

    private readonly PaymentDbContext _db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly PaymentConfig _config;
    private readonly ManualTimeProvider _timeProvider;
    private readonly FakePaymentProvider _paymentProvider;
    private readonly FakeHubContext _hubContext;
    private readonly MatchManager _matchManager;
    private readonly WalletService _walletService;
    private readonly PayoutService _sut;

    public PayoutServiceIntegrationTests()
    {
        (_db, _connection) = PaymentDbContextFactory.CreateOpen();
        _config = new PaymentConfig { CommissionRate = 0.10m };
        _timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        _paymentProvider = new FakePaymentProvider(NullLogger<FakePaymentProvider>.Instance);
        _hubContext = new FakeHubContext();

        var mapProvider = new MapProvider(new FakeHostEnvironment(), NullLogger<MapProvider>.Instance);
        _matchManager = new MatchManager(mapProvider, TestEventLog.Writer(), NullLogger<MatchManager>.Instance);
        var priceOracle = new FixedPriceOracle(FixedRateUsdPerLtc);
        _walletService = new WalletService(_db, priceOracle, _paymentProvider, Options.Create(_config), _timeProvider, NullLogger<WalletService>.Instance);

        _sut = new PayoutService(
            _db, _paymentProvider, priceOracle, _walletService, _matchManager, Options.Create(_config), _timeProvider,
            new PaymentEventNotifier(_hubContext), NullLogger<PayoutService>.Instance);
    }

    private async Task<string> SeedMatchWithConfirmedInvoicesAsync(decimal entryFeeUsd, params (string PlayerId, string PayoutAddress)[] players)
    {
        var (match, _) = MatchFactory.CreateConfirmedVipMatch(_matchManager, entryFeeUsd, players.Select(p => p.PlayerId).ToList(), _timeProvider.GetUtcNow().UtcDateTime);

        foreach (var (playerId, payoutAddress) in players)
        {
            _db.PaymentInvoices.Add(new PaymentInvoice
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                PlayerId = playerId,
                BtcPayInvoiceId = $"inv-{Guid.NewGuid():N}",
                AmountUsd = entryFeeUsd,
                AmountLtc = entryFeeUsd / FixedRateUsdPerLtc,
                LockedUsdPerLtc = FixedRateUsdPerLtc,
                PriceOracleSource = PriceOracleSource.CoinGecko,
                RateServedFromCache = false,
                RateAgeSecondsAtUse = 0,
                PayoutAddress = payoutAddress,
                PayoutAddressFormat = PayoutAddressFormat.Bech32,
                Status = PaymentInvoiceStatus.Confirmed,
                ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(15),
                CreatedAt = _timeProvider.GetUtcNow(),
                ConfirmedAt = _timeProvider.GetUtcNow()
            });
        }

        await _db.SaveChangesAsync();
        return match.Id;
    }

    [Fact]
    public async Task ProcessPayoutAsync_SingleWinner_ComputesPoolFromRoomEntryFeeTimesPlayerCount()
    {
        var matchId = await SeedMatchWithConfirmedInvoicesAsync(
            1.00m,
            ("winner", "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4"),
            ("loser", "bc1q0000000000000000000000000000000000000"));

        await _sut.ProcessPayoutAsync(matchId, ["winner"], CancellationToken.None);

        var payout = await _db.Payouts.SingleAsync();
        Assert.Equal(PayoutStatus.PayoutPending, payout.Status);
        Assert.Equal(1, payout.WinnerCount);
        // TotalPoolUsd = 1.00 × 2 oyuncu = 2.00; / 40 USD/LTC = 0.05 LTC (bkz. docs Bölüm 1.1).
        Assert.Equal(0.05000000m, payout.TotalPoolLtc);
        Assert.Equal(0.00500000m, payout.CommissionLtc);

        var recipient = await _db.PayoutRecipients.SingleAsync();
        Assert.Equal(payout.Id, recipient.PayoutId);
        Assert.Equal("winner", recipient.WinnerPlayerId);
        // GrossPerWinner = (0.05 - 0.005) / 1 = 0.045; - estimatedFee(0.0005) = 0.0445.
        Assert.Equal(0.04450000m, recipient.AmountLtc);
        Assert.Null(recipient.NetworkFeeLtc); // 🔒 Bölüm 2.6: tahmini fee asla persist edilmez.
    }

    [Fact]
    public async Task ProcessPayoutAsync_WinnerJoinedDirectlyFromWalletWithoutInvoice_StillIncludedInPoolAndPaid()
    {
        // v9 regresyon testi: "winner" hiç PaymentInvoice oluşturmadan (mevcut Wallet
        // bakiyesinden doğrudan) katılmış olsun — Wallet'ta kayıtlı bir ödül adresi var
        // ama invoice yok. Havuz yine de tam oyuncu sayısı kadar hesaplanmalı ve winner'a
        // Wallet'taki adresten gönderilmeli (eskiden invoice toplamına dayanan hesap bu
        // katkıyı sessizce kaybediyordu).
        var (match, _) = MatchFactory.CreateConfirmedVipMatch(_matchManager, 1.00m, ["winner", "loser"], _timeProvider.GetUtcNow().UtcDateTime);

        // "loser" normal şekilde invoice ile ödedi.
        _db.PaymentInvoices.Add(new PaymentInvoice
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            PlayerId = "loser",
            BtcPayInvoiceId = $"inv-{Guid.NewGuid():N}",
            AmountUsd = 1.00m,
            AmountLtc = 1.00m / FixedRateUsdPerLtc,
            LockedUsdPerLtc = FixedRateUsdPerLtc,
            PriceOracleSource = PriceOracleSource.CoinGecko,
            PayoutAddress = "bc1q0000000000000000000000000000000000000",
            PayoutAddressFormat = PayoutAddressFormat.Bech32,
            Status = PaymentInvoiceStatus.Confirmed,
            ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(15),
            CreatedAt = _timeProvider.GetUtcNow(),
            ConfirmedAt = _timeProvider.GetUtcNow()
        });
        await _db.SaveChangesAsync();

        // "winner" hiç invoice oluşturmadı — yalnızca Wallet'ta kayıtlı bir adresi var.
        await _walletService.ResolveAndSavePayoutAddressAsync("winner", "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4", CancellationToken.None);

        await _sut.ProcessPayoutAsync(match.Id, ["winner"], CancellationToken.None);

        var payout = await _db.Payouts.SingleAsync();
        // Havuz hâlâ 2 oyuncu üzerinden hesaplanıyor, "winner"ın invoice'sizliği kayba yol açmıyor.
        Assert.Equal(0.05000000m, payout.TotalPoolLtc);

        var recipient = await _db.PayoutRecipients.SingleAsync();
        Assert.Equal("winner", recipient.WinnerPlayerId);
        Assert.Equal("bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4", recipient.PayoutAddress);
        Assert.Equal(0.04450000m, recipient.AmountLtc);
    }

    [Fact]
    public async Task ProcessPayoutAsync_WinnerWithNoInvoiceAndNoWalletAddress_SkipsRecipientButStillCreatesPayout()
    {
        var (match, _) = MatchFactory.CreateConfirmedVipMatch(_matchManager, 1.00m, ["winner"], _timeProvider.GetUtcNow().UtcDateTime);

        await _sut.ProcessPayoutAsync(match.Id, ["winner"], CancellationToken.None);

        var payout = await _db.Payouts.SingleAsync();
        Assert.Equal(0.02500000m, payout.TotalPoolLtc); // 1.00 × 1 oyuncu / 40.
        Assert.Empty(await _db.PayoutRecipients.ToListAsync()); // ödül adresi yok, satır atlandı.
    }

    [Fact]
    public async Task ProcessPayoutAsync_CalledTwice_IsIdempotent_OnlyOnePayoutRow()
    {
        var matchId = await SeedMatchWithConfirmedInvoicesAsync(1.00m, ("winner", "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4"));

        await _sut.ProcessPayoutAsync(matchId, ["winner"], CancellationToken.None);
        await _sut.ProcessPayoutAsync(matchId, ["winner"], CancellationToken.None);

        Assert.Equal(1, await _db.Payouts.CountAsync());
        Assert.Equal(1, await _db.PayoutRecipients.CountAsync());
    }

    [Fact]
    public async Task ProcessPayoutAsync_TwoWinners_CreatesOnePayoutRecipientPerWinner_WithEqualShare()
    {
        var matchId = await SeedMatchWithConfirmedInvoicesAsync(
            1.00m,
            ("winner-1", "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4"),
            ("winner-2", "bc1q0000000000000000000000000000000000000"));

        await _sut.ProcessPayoutAsync(matchId, ["winner-1", "winner-2"], CancellationToken.None);

        var payout = await _db.Payouts.SingleAsync();
        Assert.Equal(2, payout.WinnerCount);

        var recipients = await _db.PayoutRecipients.Where(r => r.PayoutId == payout.Id).ToListAsync();
        Assert.Equal(2, recipients.Count);
        Assert.Contains(recipients, r => r.WinnerPlayerId == "winner-1");
        Assert.Contains(recipients, r => r.WinnerPlayerId == "winner-2");
        Assert.Equal(recipients[0].AmountLtc, recipients[1].AmountLtc); // eşit havuz -> eşit pay.
    }

    [Fact]
    public async Task FullLifecycle_PendingToSentToCompleted_WithActualFeeOnly()
    {
        var matchId = await SeedMatchWithConfirmedInvoicesAsync(1.00m, ("winner", "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4"));
        await _sut.ProcessPayoutAsync(matchId, ["winner"], CancellationToken.None);

        await _sut.ProcessDuePayoutsAsync(CancellationToken.None);
        var sentRecipient = await _db.PayoutRecipients.SingleAsync();
        Assert.Equal(PayoutStatus.PayoutSent, sentRecipient.Status);
        Assert.NotNull(sentRecipient.BtcPayTransactionId);
        Assert.Null(sentRecipient.NetworkFeeLtc);

        await _sut.ReconcileSentPayoutsAsync(CancellationToken.None);
        var completedRecipient = await _db.PayoutRecipients.SingleAsync();
        Assert.Equal(PayoutStatus.Completed, completedRecipient.Status);
        Assert.NotNull(completedRecipient.NetworkFeeLtc);
        Assert.NotNull(completedRecipient.CompletedAt);

        var payout = await _db.Payouts.SingleAsync();
        Assert.Equal(PayoutStatus.Completed, payout.Status);
        Assert.NotNull(payout.CompletedAt);
        Assert.Single(_hubContext.Proxy.Sent);
        Assert.Equal("PayoutCompleted", _hubContext.Proxy.Sent[0].Method);
    }

    [Fact]
    public async Task ReconcileSentPayouts_DoesNotOverwriteAlreadyFilledNetworkFee()
    {
        var matchId = await SeedMatchWithConfirmedInvoicesAsync(1.00m, ("winner", "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4"));
        await _sut.ProcessPayoutAsync(matchId, ["winner"], CancellationToken.None);
        await _sut.ProcessDuePayoutsAsync(CancellationToken.None);
        await _sut.ReconcileSentPayoutsAsync(CancellationToken.None);

        var firstFee = (await _db.PayoutRecipients.SingleAsync()).NetworkFeeLtc;

        // İkinci reconciliation turu: zaten Completed olduğundan bir daha işlenmemeli (kendi başına idempotency).
        await _sut.ReconcileSentPayoutsAsync(CancellationToken.None);
        var secondFee = (await _db.PayoutRecipients.SingleAsync()).NetworkFeeLtc;

        Assert.Equal(firstFee, secondFee);
        Assert.Single(_hubContext.Proxy.Sent); // yalnızca ilk turda notify edilmiş olmalı.
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
