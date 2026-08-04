using api;
using api.Models;
using api.Models.Rooms;
using api.Services;
using api.Services.GameEngine;
using api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace api.Tests;

public class MovementServiceTests
{
    private readonly MapProvider _mapProvider = new(new FakeHostEnvironment(), NullLogger<MapProvider>.Instance);
    private readonly MovementService _sut;

    public MovementServiceTests()
    {
        var combatService = new CombatService(TestEventLog.Writer(), NullLogger<CombatService>.Instance);
        _sut = new MovementService(_mapProvider, combatService, NullLogger<MovementService>.Instance);
    }

    private static (Match match, Player player, Region region) CreateOwnedRegion(string regionId, int soldiers)
    {
        var player = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var match = new Match
        {
            Id = "m1",
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
        match.Players.Add(player);

        var region = new Region { Id = regionId, OriginalOwnerId = player.Id, OwnerId = player.Id, SoldierCount = soldiers };
        match.Regions[region.Id] = region;

        return (match, player, region);
    }

    [Fact]
    public void DepartArmy_Success_SendsAllSoldiersMinusGarrisonAndCreatesArmyWithFixedDuration()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 10);
        var now = DateTime.UtcNow;

        var army = _sut.DepartArmy(match, player, region, "esch-sur-alzette", now);

        Assert.Equal(GameConfig.MinGarrisonPerSend, region.SoldierCount);
        Assert.Contains(army, match.Armies);
        Assert.Equal(10 - GameConfig.MinGarrisonPerSend, army.SoldierCount);
        Assert.Equal("esch-sur-alzette", army.ToRegionId);
        Assert.Equal(now.AddSeconds(GameConfig.MovementDurationSeconds), army.ArrivesAtUtc);
    }

    [Fact]
    public void DepartArmy_NotNeighbor_Throws()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 10);

        Assert.Throws<InvalidOperationException>(() =>
            _sut.DepartArmy(match, player, region, "remich", DateTime.UtcNow));
    }

    [Fact]
    public void DepartArmy_NotEnoughSoldiersBeyondGarrison_Throws()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: GameConfig.MinGarrisonPerSend);

        Assert.Throws<InvalidOperationException>(() =>
            _sut.DepartArmy(match, player, region, "esch-sur-alzette", DateTime.UtcNow));
    }

    [Fact]
    public void DepartArmy_NotOwner_Throws()
    {
        var (match, _, region) = CreateOwnedRegion("luxembourg-city", soldiers: 10);
        var other = new Player { Id = "p2", Slot = 1, Name = "Bob" };

        Assert.Throws<InvalidOperationException>(() =>
            _sut.DepartArmy(match, other, region, "esch-sur-alzette", DateTime.UtcNow));
    }

    [Fact]
    public void ProcessArrivals_ReinforcementToOwnRegion_MergesGarrisonWithoutCombat()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 10);
        var targetRegion = new Region { Id = "esch-sur-alzette", OriginalOwnerId = player.Id, OwnerId = player.Id, SoldierCount = 1 };
        match.Regions[targetRegion.Id] = targetRegion;

        var now = DateTime.UtcNow;
        var army = _sut.DepartArmy(match, player, region, "esch-sur-alzette", now);
        army.ArrivesAtUtc = now.AddSeconds(-1);

        var arrived = _sut.ProcessArrivals(match, DateTime.UtcNow);

        Assert.Single(arrived);
        Assert.Equal(1 + (10 - GameConfig.MinGarrisonPerSend), targetRegion.SoldierCount);
        Assert.Empty(match.Armies);
    }

    [Fact]
    public void ProcessArrivals_ToEnemyRegion_TriggersCombatCaptureAndAllSurvivorsBecomeGarrison()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 5);
        var enemy = new Player { Id = "p2", Slot = 1, Name = "Bob" };
        match.Players.Add(enemy);
        var enemyRegion = new Region { Id = "esch-sur-alzette", OriginalOwnerId = enemy.Id, OwnerId = enemy.Id, SoldierCount = 1 };
        match.Regions[enemyRegion.Id] = enemyRegion;

        var now = DateTime.UtcNow;
        var army = _sut.DepartArmy(match, player, region, "esch-sur-alzette", now);
        army.ArrivesAtUtc = now.AddSeconds(-1);

        _sut.ProcessArrivals(match, DateTime.UtcNow);

        // 5 asker gönderilir (5 - MinGarrisonPerSend=1 -> 4 gönderilir), savunma 1'i yener,
        // kalan 3 asker garrison olarak kalır (otomatik zincirleme yok, tek hop).
        Assert.Equal(player.Id, enemyRegion.OwnerId);
        Assert.Equal(3, enemyRegion.SoldierCount);
    }
}
