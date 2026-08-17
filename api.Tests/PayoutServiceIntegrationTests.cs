using api.Services;
using api.Services.Payments;
using api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api.Tests;

/// <summary>
/// 2026-08-08 kararı: payout artık on-chain LTC gönderimi değil, senkron bir
/// Wallet.BalanceUsd kredisidir (bkz. PayoutService.ProcessPayoutAsync). Havuz
/// Room.EntryFeeUsd × onaylı (insan) oyuncu sayısından hesaplanır — bir
/// PaymentInvoice'ın var olup olmaması artık havuzu veya kazanan başına ödemeyi
/// hiç etkilemez (bkz. docs/05-payment.md Bölüm 1.1).
/// </summary>
public class PayoutServiceIntegrationTests : IDisposable
{
    private readonly PaymentDbContext _db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly PaymentConfig _config;
    private readonly ManualTimeProvider _timeProvider;
    private readonly FakeHubContext _hubContext;
    private readonly MatchManager _matchManager;
    private readonly WalletService _walletService;
    private readonly PayoutService _sut;

    public PayoutServiceIntegrationTests()
    {
        (_db, _connection) = PaymentDbContextFactory.CreateOpen();
        _config = new PaymentConfig { CommissionRate = 0.10m };
        _timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        _hubContext = new FakeHubContext();

        var mapProvider = new MapProvider(new FakeHostEnvironment(), NullLogger<MapProvider>.Instance);
        _matchManager = new MatchManager(mapProvider, TestEventLog.Writer(), NullLogger<MatchManager>.Instance);
        var paymentProvider = new FakePaymentProvider(NullLogger<FakePaymentProvider>.Instance);
        var notifier = new PaymentEventNotifier(_hubContext, new FakeWalletHubContext());
        _walletService = new WalletService(
            _db, new FixedPriceOracle(40.0m), paymentProvider, Options.Create(_config), _timeProvider, notifier, NullLogger<WalletService>.Instance);

        _sut = new PayoutService(
            _db, _walletService, _matchManager, Options.Create(_config), _timeProvider,
            notifier, NullLogger<PayoutService>.Instance);
    }

    [Fact]
    public async Task ProcessPayoutAsync_SingleWinner_ComputesPoolFromRoomEntryFeeTimesConfirmedPlayerCount_AndCreditsWinner()
    {
        var (match, _) = MatchFactory.CreateConfirmedVipMatch(_matchManager, 1.00m, ["winner", "loser"], _timeProvider.GetUtcNow().UtcDateTime);

        await _sut.ProcessPayoutAsync(match.Id, ["winner"], CancellationToken.None);

        var payout = await _db.Payouts.SingleAsync();
        Assert.Equal(1, payout.WinnerCount);
        // TotalPoolUsd = 1.00 × 2 oyuncu = 2.00
        Assert.Equal(2.00m, payout.TotalPoolUsd);
        Assert.Equal(0.20m, payout.CommissionUsd);

        var recipient = await _db.PayoutRecipients.SingleAsync();
        Assert.Equal(payout.Id, recipient.PayoutId);
        Assert.Equal("winner", recipient.WinnerPlayerId);
        // GrossPerWinner = (2.00 - 0.20) / 1 = 1.80
        Assert.Equal(1.80m, recipient.AmountUsd);
        Assert.Equal(1.80m, await _walletService.GetBalanceAsync("winner", CancellationToken.None));
    }

    [Fact]
    public async Task ProcessPayoutAsync_CalledTwice_IsIdempotent_OnlyOnePayoutRow_AndOnlyOneCredit()
    {
        var (match, _) = MatchFactory.CreateConfirmedVipMatch(_matchManager, 1.00m, ["winner"], _timeProvider.GetUtcNow().UtcDateTime);

        await _sut.ProcessPayoutAsync(match.Id, ["winner"], CancellationToken.None);
        await _sut.ProcessPayoutAsync(match.Id, ["winner"], CancellationToken.None);

        Assert.Equal(1, await _db.Payouts.CountAsync());
        Assert.Equal(1, await _db.PayoutRecipients.CountAsync());
        Assert.Equal(0.90m, await _walletService.GetBalanceAsync("winner", CancellationToken.None)); // (1.00 - 0.10)/1, yalnızca bir kez.
    }

    [Fact]
    public async Task ProcessPayoutAsync_TwoWinners_CreatesOnePayoutRecipientPerWinner_WithEqualShare()
    {
        var (match, _) = MatchFactory.CreateConfirmedVipMatch(_matchManager, 1.00m, ["winner-1", "winner-2"], _timeProvider.GetUtcNow().UtcDateTime);

        await _sut.ProcessPayoutAsync(match.Id, ["winner-1", "winner-2"], CancellationToken.None);

        var payout = await _db.Payouts.SingleAsync();
        Assert.Equal(2, payout.WinnerCount);

        var recipients = await _db.PayoutRecipients.Where(r => r.PayoutId == payout.Id).ToListAsync();
        Assert.Equal(2, recipients.Count);
        Assert.Contains(recipients, r => r.WinnerPlayerId == "winner-1");
        Assert.Contains(recipients, r => r.WinnerPlayerId == "winner-2");
        Assert.Equal(recipients[0].AmountUsd, recipients[1].AmountUsd); // eşit havuz -> eşit pay.
        Assert.Equal(0.90m, await _walletService.GetBalanceAsync("winner-1", CancellationToken.None));
        Assert.Equal(0.90m, await _walletService.GetBalanceAsync("winner-2", CancellationToken.None));
    }

    [Fact]
    public async Task ProcessPayoutAsync_NotifiesPayoutCompletedOverSignalR()
    {
        var (match, _) = MatchFactory.CreateConfirmedVipMatch(_matchManager, 1.00m, ["winner"], _timeProvider.GetUtcNow().UtcDateTime);

        await _sut.ProcessPayoutAsync(match.Id, ["winner"], CancellationToken.None);

        Assert.Single(_hubContext.Proxy.Sent);
        Assert.Equal("PayoutCompleted", _hubContext.Proxy.Sent[0].Method);
    }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 7 (DÜZELTME — bot politikası): bot her zaman
    /// IsPaymentConfirmed=true olur (maç başlayabilsin diye), ama hiçbir zaman
    /// gerçek para yatırmaz — havuz yalnızca insan oyuncuların katkısından
    /// hesaplanmalı, botun "hayali" katkısı insanların payını şişirmemeli.
    /// </summary>
    [Fact]
    public async Task ProcessPayoutAsync_MatchHasConfirmedBotPlayer_BotExcludedFromPoolCalculation()
    {
        var (match, _) = MatchFactory.CreateConfirmedVipMatch(_matchManager, 1.00m, ["winner"], _timeProvider.GetUtcNow().UtcDateTime);
        match.Players.Add(new api.Models.Player
        {
            Id = "bot-1",
            Slot = 1,
            Name = "Bot 1",
            IsBot = true,
            BotDifficulty = api.Models.BotDifficulty.Normal,
            IsPaymentConfirmed = true
        });

        await _sut.ProcessPayoutAsync(match.Id, ["winner"], CancellationToken.None);

        var payout = await _db.Payouts.SingleAsync();
        // Bot dahil edilseydi havuz 2 × 1.00 = 2.00 olurdu; yalnızca insan oyuncu (1) sayılınca 1.00 olmalı.
        Assert.Equal(1.00m, payout.TotalPoolUsd);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
