using api;
using api.Models;
using api.Services.GameEngine;
using Microsoft.Extensions.Logging.Abstractions;

namespace api.Tests;

public class UpgradeServiceTests
{
    private readonly UpgradeService _sut = new(NullLogger<UpgradeService>.Instance);

    private static (Player player, Region region) CreateOwnedRegion(int gold, int nestLevel)
    {
        var player = new Player { Id = "p1", Slot = 0, Name = "Alice", Gold = gold };
        var region = new Region
        {
            Id = "clervaux",
            OwnerId = player.Id,
            Nest = new Nest { RegionId = "clervaux", OwnerId = player.Id, Level = nestLevel }
        };
        return (player, region);
    }

    [Fact]
    public void Upgrade_Level1To2_DeductsCostAndGrantsArcher()
    {
        var (player, region) = CreateOwnedRegion(gold: GameConfig.NestUpgradeToLevel2Cost, nestLevel: 1);

        _sut.Upgrade(player, region);

        Assert.Equal(2, region.Nest!.Level);
        Assert.Equal(0, player.Gold);
        Assert.Equal(GameConfig.NestLevel2ArcherCount, region.Nest.GarrisonArchers);
    }

    [Fact]
    public void Upgrade_Level2To3_GrantsFullArcherCount()
    {
        var (player, region) = CreateOwnedRegion(gold: GameConfig.NestUpgradeToLevel3Cost, nestLevel: 2);

        _sut.Upgrade(player, region);

        Assert.Equal(3, region.Nest!.Level);
        Assert.Equal(GameConfig.NestLevel3ArcherCount, region.Nest.GarrisonArchers);
    }

    [Fact]
    public void Upgrade_InsufficientGold_Throws()
    {
        var (player, region) = CreateOwnedRegion(gold: 100, nestLevel: 1);

        Assert.Throws<InvalidOperationException>(() => _sut.Upgrade(player, region));
        Assert.Equal(1, region.Nest!.Level);
    }

    [Fact]
    public void Upgrade_AlreadyMaxLevel_Throws()
    {
        var (player, region) = CreateOwnedRegion(gold: 999_999, nestLevel: GameConfig.MaxNestLevel);

        Assert.Throws<InvalidOperationException>(() => _sut.Upgrade(player, region));
    }

    [Fact]
    public void Upgrade_NotOwner_Throws()
    {
        var (player, region) = CreateOwnedRegion(gold: 999_999, nestLevel: 1);
        region.OwnerId = "someone-else";

        Assert.Throws<InvalidOperationException>(() => _sut.Upgrade(player, region));
    }
}
