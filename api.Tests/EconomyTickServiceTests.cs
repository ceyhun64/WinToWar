using api;
using api.Hubs;
using api.Models;
using api.Models.Rooms;
using api.Services;
using api.Services.GameEngine;
using Microsoft.Extensions.Logging.Abstractions;

namespace api.Tests;

public class EconomyTickServiceTests
{
    private readonly EconomyTickService _sut;

    public EconomyTickServiceTests()
    {
        var mapProvider = new MapProvider(new TestSupport.FakeHostEnvironment(), NullLogger<MapProvider>.Instance);
        var eventLogWriter = TestSupport.TestEventLog.Writer();
        var matchManager = new MatchManager(mapProvider, eventLogWriter, NullLogger<MatchManager>.Instance);
        var combatService = new CombatService(eventLogWriter, NullLogger<CombatService>.Instance);
        var movementService = new MovementService(mapProvider, combatService, NullLogger<MovementService>.Instance);
        _sut = new EconomyTickService(
            matchManager, movementService, eventLogWriter,
            hubContext: null!, scopeFactory: null!, NullLogger<EconomyTickService>.Instance);
    }

    private static Match CreateMatch(DateTime startedAt, params Player[] players)
    {
        var match = new Match
        {
            Id = "m1",
            Status = MatchStatus.Playing,
            StartedAtUtc = startedAt,
            Room = new Room
            {
                Id = "r1",
                Type = RoomType.Standard,
                MaxPlayers = 4,
                GreyRegionDefenseCount = 1,
                FogOfWar = false,
                EntryFeeUsd = 1.00m,
                CreatorPlayerId = "creator"
            }
        };
        match.Players.AddRange(players);
        return match;
    }

    [Fact]
    public void Tick_TenSecondsElapsed_ProducesExactlyOneInterval()
    {
        var player = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var start = DateTime.UtcNow;
        var match = CreateMatch(start, player);
        var home = new Region { Id = "home", OriginalOwnerId = player.Id, OwnerId = player.Id, SoldierCount = 0 };
        match.Regions[home.Id] = home;

        var now = start;
        for (var i = 0; i < GameConfig.ProductionIntervalSeconds; i++)
        {
            now = now.AddSeconds(1);
            _sut.Tick(match, now);
        }

        Assert.Equal(GameConfig.BaseProductionPerInterval, home.SoldierCount);
    }

    [Fact]
    public void Tick_PartialInterval_DoesNotProduceYet()
    {
        var player = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var start = DateTime.UtcNow;
        var match = CreateMatch(start, player);
        var home = new Region { Id = "home", OriginalOwnerId = player.Id, OwnerId = player.Id, SoldierCount = 0 };
        match.Regions[home.Id] = home;

        var now = start;
        for (var i = 0; i < GameConfig.ProductionIntervalSeconds - 1; i++)
        {
            now = now.AddSeconds(1);
            _sut.Tick(match, now);
        }

        Assert.Equal(0, home.SoldierCount);
    }

    [Fact]
    public void Tick_ConqueredRegionsAddBonusToProduction()
    {
        var player = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var start = DateTime.UtcNow;
        var match = CreateMatch(start, player);
        var home = new Region { Id = "home", OriginalOwnerId = player.Id, OwnerId = player.Id, SoldierCount = 0 };
        var conquered1 = new Region { Id = "c1", OriginalOwnerId = null, OwnerId = player.Id, SoldierCount = 0 };
        var conquered2 = new Region { Id = "c2", OriginalOwnerId = null, OwnerId = player.Id, SoldierCount = 0 };
        match.Regions[home.Id] = home;
        match.Regions[conquered1.Id] = conquered1;
        match.Regions[conquered2.Id] = conquered2;

        _sut.Tick(match, start.AddSeconds(GameConfig.ProductionIntervalSeconds));

        // BaseProduction(4) + 2 fethedilmiş bölge * BonusPerRegion(1) = 6.
        Assert.Equal(GameConfig.BaseProductionPerInterval + 2 * GameConfig.ProductionBonusPerRegion, home.SoldierCount);
    }

    [Fact]
    public void Tick_LostHomeRegionOwner_StopsProducing()
    {
        var player = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var start = DateTime.UtcNow;
        var match = CreateMatch(start, player);
        // Ev bölgesi başka bir oyuncunun elinde (ele geçirilmiş) -> artık üretmez.
        var home = new Region { Id = "home", OriginalOwnerId = player.Id, OwnerId = "someone-else", SoldierCount = 0 };
        match.Regions[home.Id] = home;

        _sut.Tick(match, start.AddSeconds(GameConfig.ProductionIntervalSeconds));

        Assert.Equal(0, home.SoldierCount);
    }

    [Fact]
    public void Tick_OnlyOnePlayerRemaining_EndsMatchWithThatPlayerAsWinner()
    {
        var winner = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var eliminated = new Player { Id = "p2", Slot = 1, Name = "Bob", IsEliminated = true };
        var match = CreateMatch(DateTime.UtcNow, winner, eliminated);

        _sut.Tick(match, DateTime.UtcNow);

        Assert.Equal(MatchStatus.Completed, match.Status);
        Assert.Equal([winner.Id], match.Winners);
    }

    [Fact]
    public void Tick_LastTwoPlayersAbandonSimultaneously_BothAreJointWinners()
    {
        var p1 = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var p2 = new Player { Id = "p2", Slot = 1, Name = "Bob" };
        var match = CreateMatch(DateTime.UtcNow, p1, p2);

        var now = DateTime.UtcNow;
        p1.DisconnectedAtUtc = now.AddSeconds(-GameConfig.AbandonmentTimeoutSeconds - 1);
        p2.DisconnectedAtUtc = now.AddSeconds(-GameConfig.AbandonmentTimeoutSeconds - 1);

        _sut.Tick(match, now);

        Assert.Equal(MatchStatus.Completed, match.Status);
        Assert.Equal(2, match.Winners.Count);
        Assert.Contains(p1.Id, match.Winners);
        Assert.Contains(p2.Id, match.Winners);
    }
}
