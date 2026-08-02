using api;
using api.Models;
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
        var combatService = new CombatService(NullLogger<CombatService>.Instance);
        _sut = new MovementService(_mapProvider, combatService, NullLogger<MovementService>.Instance);
    }

    private (Match match, Player player, Region region, General general) CreateOwnedRegionWithGeneral(
        string regionId, int soldiers)
    {
        var player = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var match = new Match { Id = "m1" };
        match.Players.Add(player);

        var region = new Region
        {
            Id = regionId,
            OwnerId = player.Id,
            Nest = new Nest { RegionId = regionId, OwnerId = player.Id, Level = 1, GarrisonSoldiers = soldiers }
        };
        match.Regions[region.Id] = region;

        var general = new General { Id = "g1", OwnerId = player.Id, Status = GeneralStatus.Garrisoned, CurrentRegionId = region.Id };
        match.Generals.Add(general);

        return (match, player, region, general);
    }

    [Fact]
    public void GetTravelTimeSeconds_ShortDistance_IsClampedToMinimum()
    {
        // clervaux-diekirch mesafesi 3.0 * 1.5 = 4.5 -> min 5'e clamp edilir.
        var seconds = _sut.GetTravelTimeSeconds("clervaux", "diekirch");

        Assert.Equal(GameConfig.MinTravelTimeSeconds, seconds);
    }

    [Fact]
    public void GetTravelTimeSeconds_WithinRange_IsNotClamped()
    {
        // clervaux-wiltz mesafesi 4.5 * 1.5 = 6.75, [5,15] aralığında.
        var seconds = _sut.GetTravelTimeSeconds("clervaux", "wiltz");

        Assert.Equal(6.75, seconds, precision: 3);
    }

    [Fact]
    public void DepartArmy_Success_DeductsGarrisonAndCreatesArmy()
    {
        var (match, player, region, general) = CreateOwnedRegionWithGeneral("clervaux", soldiers: 10);

        var army = _sut.DepartArmy(match, player, general, region, "wiltz", soldierCount: 4);

        Assert.Equal(6, region.Nest!.GarrisonSoldiers);
        Assert.Equal(GeneralStatus.Moving, general.Status);
        Assert.Null(general.CurrentRegionId);
        Assert.Contains(army, match.Armies);
        Assert.Equal(4, army.SoldierCount);
    }

    [Fact]
    public void DepartArmy_NotNeighbor_Throws()
    {
        var (match, player, region, general) = CreateOwnedRegionWithGeneral("clervaux", soldiers: 10);

        Assert.Throws<InvalidOperationException>(() =>
            _sut.DepartArmy(match, player, general, region, "dudelange", soldierCount: 4));
    }

    [Fact]
    public void DepartArmy_InsufficientSoldiers_Throws()
    {
        var (match, player, region, general) = CreateOwnedRegionWithGeneral("clervaux", soldiers: 2);

        Assert.Throws<InvalidOperationException>(() =>
            _sut.DepartArmy(match, player, general, region, "wiltz", soldierCount: 4));
    }

    [Fact]
    public void DepartArmy_GeneralNotGarrisonedHere_Throws()
    {
        var (match, player, region, general) = CreateOwnedRegionWithGeneral("clervaux", soldiers: 10);
        general.Status = GeneralStatus.Moving;

        Assert.Throws<InvalidOperationException>(() =>
            _sut.DepartArmy(match, player, general, region, "wiltz", soldierCount: 4));
    }

    [Fact]
    public void ProcessArrivals_ReinforcementToOwnRegion_MergesGarrisonWithoutCombat()
    {
        var (match, player, region, general) = CreateOwnedRegionWithGeneral("clervaux", soldiers: 10);
        var targetRegion = new Region
        {
            Id = "wiltz",
            OwnerId = player.Id,
            Nest = new Nest { RegionId = "wiltz", OwnerId = player.Id, Level = 1, GarrisonSoldiers = 1 }
        };
        match.Regions[targetRegion.Id] = targetRegion;

        var army = _sut.DepartArmy(match, player, general, region, "wiltz", soldierCount: 4);
        army.ArrivesAtUtc = DateTime.UtcNow.AddSeconds(-1); // varmış gibi ayarla

        var arrived = _sut.ProcessArrivals(match);

        Assert.Single(arrived);
        Assert.Equal(5, targetRegion.Nest!.GarrisonSoldiers);
        Assert.Equal(GeneralStatus.Garrisoned, general.Status);
        Assert.Equal("wiltz", general.CurrentRegionId);
        Assert.Empty(match.Armies);
    }

    [Fact]
    public void ProcessArrivals_ToNeutralRegion_TriggersCombatCapture()
    {
        var (match, player, region, general) = CreateOwnedRegionWithGeneral("clervaux", soldiers: 10);
        var neutralRegion = new Region { Id = "wiltz", NeutralDefenseSoldiers = GameConfig.NeutralRegionDefenseSoldiers };
        match.Regions[neutralRegion.Id] = neutralRegion;

        var army = _sut.DepartArmy(match, player, general, region, "wiltz", soldierCount: 4);
        army.ArrivesAtUtc = DateTime.UtcNow.AddSeconds(-1);

        _sut.ProcessArrivals(match);

        Assert.Equal(player.Id, neutralRegion.OwnerId);
        Assert.Equal(GeneralStatus.Garrisoned, general.Status);
    }
}
