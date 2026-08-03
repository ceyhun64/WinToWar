using api;
using api.Hubs;
using api.Models;
using api.Services;
using api.Services.GameEngine;
using api.Tests.TestSupport;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

namespace api.Tests;

public class EconomyTickServiceTests
{
    private readonly EconomyTickService _sut;

    public EconomyTickServiceTests()
    {
        var mapProvider = new MapProvider(new FakeHostEnvironment(), NullLogger<MapProvider>.Instance);
        var matchManager = new MatchManager(mapProvider, NullLogger<MatchManager>.Instance);
        var combatService = new CombatService(NullLogger<CombatService>.Instance);
        var movementService = new MovementService(mapProvider, combatService, NullLogger<MovementService>.Instance);
        _sut = new EconomyTickService(matchManager, movementService, hubContext: null!, scopeFactory: null!, NullLogger<EconomyTickService>.Instance);
    }

    private static (Match match, Player player, Region region) CreateMatchWithNest(int nestLevel, double startingGold = 0)
    {
        var player = new Player { Id = "p1", Slot = 0, Name = "Alice", Gold = startingGold };
        var match = new Match { Id = "m1", Status = MatchStatus.InProgress, StartedAtUtc = DateTime.UtcNow };
        match.Players.Add(player);

        var region = new Region
        {
            Id = "clervaux",
            OwnerId = player.Id,
            Nest = new Nest { RegionId = "clervaux", OwnerId = player.Id, Level = nestLevel }
        };
        match.Regions[region.Id] = region;

        return (match, player, region);
    }

    [Fact]
    public void Tick_Level1Nest_SixtySeconds_ProducesExactlyOneSoldierAndFullMinuteGold()
    {
        var (match, player, region) = CreateMatchWithNest(nestLevel: 1);

        for (var i = 0; i < 60; i++)
        {
            _sut.Tick(match);
        }

        Assert.Equal(1, region.Nest!.GarrisonSoldiers);
        Assert.Equal(GameConfig.NestLevel1GoldPerMinute, player.Gold, precision: 6);
    }

    [Fact]
    public void Tick_PartialMinute_DoesNotProduceFractionalSoldier()
    {
        var (match, _, region) = CreateMatchWithNest(nestLevel: 1);

        for (var i = 0; i < 30; i++)
        {
            _sut.Tick(match);
        }

        Assert.Equal(0, region.Nest!.GarrisonSoldiers);
    }

    [Fact]
    public void Tick_DeadGeneralWithEnoughGoldAndRespawnTimeElapsed_RespawnsAtHighestLevelNest()
    {
        var (match, player, region) = CreateMatchWithNest(nestLevel: 1, startingGold: GameConfig.GeneralRespawnCost);
        var general = new General
        {
            Id = "g1",
            OwnerId = player.Id,
            Status = GeneralStatus.Dead,
            RespawnAtUtc = DateTime.UtcNow.AddSeconds(-1)
        };
        match.Generals.Add(general);

        _sut.Tick(match);

        Assert.Equal(GeneralStatus.Garrisoned, general.Status);
        Assert.Equal(region.Id, general.CurrentRegionId);
        Assert.Null(general.RespawnAtUtc);
        Assert.True(player.Gold < GameConfig.GeneralRespawnCost);
    }

    [Fact]
    public void Tick_DeadGeneralWithoutEnoughGold_StaysDead()
    {
        var (match, _, _) = CreateMatchWithNest(nestLevel: 1, startingGold: 0);
        var general = new General
        {
            Id = "g1",
            OwnerId = match.Players[0].Id,
            Status = GeneralStatus.Dead,
            RespawnAtUtc = DateTime.UtcNow.AddSeconds(-1)
        };
        match.Generals.Add(general);

        _sut.Tick(match);

        Assert.Equal(GeneralStatus.Dead, general.Status);
    }

    [Fact]
    public void Tick_OnlyOnePlayerRemaining_EndsMatchWithThatPlayerAsWinner()
    {
        var (match, winner, _) = CreateMatchWithNest(nestLevel: 1);
        var eliminated = new Player { Id = "p2", Slot = 1, Name = "Bob", IsEliminated = true };
        match.Players.Add(eliminated);

        _sut.Tick(match);

        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Equal(winner.Id, match.WinnerId);
    }

    [Fact]
    public void Tick_TimeLimitReached_MostRegionsWins()
    {
        var (match, leader, _) = CreateMatchWithNest(nestLevel: 1);
        match.StartedAtUtc = DateTime.UtcNow.AddSeconds(-(GameConfig.MatchDurationSeconds + 1));
        var trailing = new Player { Id = "p2", Slot = 1, Name = "Bob" };
        match.Players.Add(trailing);
        // leader "clervaux" + "wiltz" (2 bölge) sahibi, trailing yalnızca "dudelange" (1 bölge) sahibi.
        match.Regions["wiltz"] = new Region { Id = "wiltz", OwnerId = leader.Id };
        match.Regions["dudelange"] = new Region { Id = "dudelange", OwnerId = trailing.Id };

        _sut.Tick(match);

        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Equal(leader.Id, match.WinnerId);
    }

    [Fact]
    public void Tick_TimeLimitReached_EqualRegions_EndsInDraw()
    {
        var (match, _, _) = CreateMatchWithNest(nestLevel: 1);
        match.StartedAtUtc = DateTime.UtcNow.AddSeconds(-(GameConfig.MatchDurationSeconds + 1));
        var other = new Player { Id = "p2", Slot = 1, Name = "Bob" };
        match.Players.Add(other);
        match.Regions["dudelange"] = new Region { Id = "dudelange", OwnerId = other.Id };

        _sut.Tick(match);

        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Null(match.WinnerId);
    }
}
