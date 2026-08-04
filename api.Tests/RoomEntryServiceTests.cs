using api.Models;
using api.Models.Rooms;
using api.Services;
using api.Services.Payments;
using api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api.Tests;

/// <summary>docs/05-payment.md Bölüm 1.9: Wallet ↔ MatchManager entegrasyon noktası — katılım/rollback davranışı.</summary>
public class RoomEntryServiceTests : IDisposable
{
    private const string ValidAddress = "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4";

    private readonly PaymentDbContext _db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly MatchManager _matchManager;
    private readonly WalletService _walletService;
    private readonly ManualTimeProvider _timeProvider;
    private readonly RoomEntryService _sut;

    public RoomEntryServiceTests()
    {
        (_db, _connection) = PaymentDbContextFactory.CreateOpen();
        var mapProvider = new MapProvider(new FakeHostEnvironment(), NullLogger<MapProvider>.Instance);
        _matchManager = new MatchManager(mapProvider, TestEventLog.Writer(), NullLogger<MatchManager>.Instance);
        _timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        _walletService = new WalletService(
            _db, new FixedPriceOracle(44.5m), new FakePaymentProvider(NullLogger<FakePaymentProvider>.Instance),
            Options.Create(new PaymentConfig()), _timeProvider, NullLogger<WalletService>.Instance);
        _sut = new RoomEntryService(_matchManager, _walletService, NullLogger<RoomEntryService>.Instance);
    }

    private Match CreateRoom(decimal entryFeeUsd, int maxPlayers = 2, RoomType type = RoomType.Vip) =>
        _matchManager.CreateMatch(new Room
        {
            Id = "r1",
            Type = type,
            MaxPlayers = maxPlayers,
            GreyRegionDefenseCount = 1,
            FogOfWar = false,
            EntryFeeUsd = entryFeeUsd,
            CreatorPlayerId = "creator"
        }, DateTime.UtcNow);

    [Fact]
    public async Task TryJoinAsync_SufficientBalance_DebitsWalletAndReservesConfirmedPlayer()
    {
        var match = CreateRoom(entryFeeUsd: 1.00m);
        await _walletService.CreditAsync("p1", 3.00m, CancellationToken.None);

        var result = await _sut.TryJoinAsync(match.Id, "p1", "Alice", ValidAddress, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(RoomEntryOutcome.Joined, result.Outcome);
        Assert.Equal(2.00m, await _walletService.GetBalanceAsync("p1", CancellationToken.None));
        Assert.Contains(match.Players, p => p.Id == "p1" && p.IsPaymentConfirmed);
    }

    [Fact]
    public async Task TryJoinAsync_NoPayoutAddressSuppliedOrOnFile_ReturnsPayoutAddressRequired_DoesNotTouchWalletOrReserve()
    {
        var match = CreateRoom(entryFeeUsd: 1.00m);
        await _walletService.CreditAsync("p1", 3.00m, CancellationToken.None);

        var result = await _sut.TryJoinAsync(match.Id, "p1", "Alice", null, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(RoomEntryOutcome.PayoutAddressRequired, result.Outcome);
        Assert.Empty(match.Players);
        Assert.Equal(3.00m, await _walletService.GetBalanceAsync("p1", CancellationToken.None));
    }

    [Fact]
    public async Task TryJoinAsync_InvalidPayoutAddress_ReturnsInvalidPayoutAddress()
    {
        var match = CreateRoom(entryFeeUsd: 1.00m);
        await _walletService.CreditAsync("p1", 3.00m, CancellationToken.None);

        var result = await _sut.TryJoinAsync(match.Id, "p1", "Alice", "not-a-real-address", DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(RoomEntryOutcome.InvalidPayoutAddress, result.Outcome);
        Assert.Empty(match.Players);
    }

    [Fact]
    public async Task TryJoinAsync_PayoutAddressAlreadyOnFile_NotSuppliedAgain_StillJoins()
    {
        var match = CreateRoom(entryFeeUsd: 1.00m);
        await _walletService.CreditAsync("p1", 3.00m, CancellationToken.None);
        await _walletService.ResolveAndSavePayoutAddressAsync("p1", ValidAddress, CancellationToken.None);

        var result = await _sut.TryJoinAsync(match.Id, "p1", "Alice", null, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(RoomEntryOutcome.Joined, result.Outcome);
    }

    [Fact]
    public async Task TryJoinAsync_InsufficientBalance_ReturnsShortfallAndDoesNotReserve()
    {
        var match = CreateRoom(entryFeeUsd: 1.00m);
        await _walletService.CreditAsync("p1", 0.40m, CancellationToken.None);

        var result = await _sut.TryJoinAsync(match.Id, "p1", "Alice", ValidAddress, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(RoomEntryOutcome.InsufficientBalance, result.Outcome);
        Assert.Equal(0.60m, result.ShortfallUsd);
        Assert.Empty(match.Players);
        Assert.Equal(0.40m, await _walletService.GetBalanceAsync("p1", CancellationToken.None)); // bakiyeye dokunulmadı
    }

    [Fact]
    public async Task TryJoinAsync_RoomFull_RefundsDebitedAmountAndReturnsRoomFull()
    {
        var match = CreateRoom(entryFeeUsd: 1.00m, maxPlayers: 1);
        _matchManager.ReservePlayer(match, "Existing");
        await _walletService.CreditAsync("p2", 5.00m, CancellationToken.None);

        var result = await _sut.TryJoinAsync(match.Id, "p2", "Bob", ValidAddress, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(RoomEntryOutcome.RoomFull, result.Outcome);
        Assert.Equal(5.00m, await _walletService.GetBalanceAsync("p2", CancellationToken.None)); // geri eklendi
    }

    [Fact]
    public async Task TryJoinAsync_FreeRoom_JoinsWithoutTouchingWallet()
    {
        var match = CreateRoom(entryFeeUsd: 0m);

        var result = await _sut.TryJoinAsync(match.Id, "p1", "Alice", ValidAddress, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(RoomEntryOutcome.Joined, result.Outcome);
        Assert.Equal(0m, await _walletService.GetBalanceAsync("p1", CancellationToken.None));
    }

    [Fact]
    public async Task TryJoinAsync_PracticeRoom_NeverRequiresPayoutAddress()
    {
        var match = CreateRoom(entryFeeUsd: 0m, type: RoomType.Practice);

        var result = await _sut.TryJoinAsync(match.Id, "p1", "Alice", null, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(RoomEntryOutcome.Joined, result.Outcome);
    }

    [Fact]
    public async Task TryJoinAsync_AlreadyReservedPlayer_ConfirmsWithoutDuplicateReservation()
    {
        // VIP kurucusu senaryosu: oda oluşturulurken zaten (onaysız) rezerve edilmiştir.
        var match = CreateRoom(entryFeeUsd: 1.00m);
        var creator = _matchManager.ReservePlayer(match, "Creator", forcedPlayerId: "creator");
        await _walletService.CreditAsync("creator", 1.00m, CancellationToken.None);

        var result = await _sut.TryJoinAsync(match.Id, "creator", "Creator", ValidAddress, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(RoomEntryOutcome.Joined, result.Outcome);
        Assert.Single(match.Players);
        Assert.True(creator.IsPaymentConfirmed);
        Assert.Equal(0m, await _walletService.GetBalanceAsync("creator", CancellationToken.None));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
