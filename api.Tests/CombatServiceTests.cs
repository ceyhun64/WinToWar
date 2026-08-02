using api;
using api.Models;
using api.Services.GameEngine;
using Microsoft.Extensions.Logging.Abstractions;

namespace api.Tests;

public class CombatServiceTests
{
    private readonly CombatService _sut = new(NullLogger<CombatService>.Instance);

    private static Match CreateMatch(params Player[] players)
    {
        var match = new Match { Id = "m1" };
        match.Players.AddRange(players);
        return match;
    }

    private static Army CreateArmy(string ownerId, string generalId, int soldierCount, string toRegionId) => new()
    {
        Id = "a1",
        OwnerId = ownerId,
        GeneralId = generalId,
        SoldierCount = soldierCount,
        FromRegionId = "from",
        ToRegionId = toRegionId,
        DepartedAtUtc = DateTime.UtcNow,
        ArrivesAtUtc = DateTime.UtcNow
    };

    [Fact]
    public void ResolveAttack_AttackerStrongerThanNeutral_CapturesRegionWithSurvivors()
    {
        var attacker = new Player { Id = "attacker", Slot = 0, Name = "Alice" };
        var match = CreateMatch(attacker);
        var region = new Region { Id = "wiltz", NeutralDefenseSoldiers = GameConfig.NeutralRegionDefenseSoldiers };
        var general = new General { Id = "g1", OwnerId = attacker.Id, Status = GeneralStatus.Moving };
        var army = CreateArmy(attacker.Id, general.Id, soldierCount: 5, toRegionId: region.Id);

        _sut.ResolveAttack(match, army, region, general);

        Assert.Equal(attacker.Id, region.OwnerId);
        Assert.NotNull(region.Nest);
        Assert.Equal(1, region.Nest!.Level);
        Assert.Equal(5 - GameConfig.NeutralRegionDefenseSoldiers, region.Nest.GarrisonSoldiers);
        Assert.Equal(GeneralStatus.Garrisoned, general.Status);
        Assert.Equal(region.Id, general.CurrentRegionId);
    }

    [Fact]
    public void ResolveAttack_AttackerWeakerThanNeutral_IsRepelledAndGeneralDies()
    {
        var attacker = new Player { Id = "attacker", Slot = 0, Name = "Alice" };
        var match = CreateMatch(attacker);
        var region = new Region { Id = "wiltz", NeutralDefenseSoldiers = 5 };
        var general = new General { Id = "g1", OwnerId = attacker.Id, Status = GeneralStatus.Moving };
        var army = CreateArmy(attacker.Id, general.Id, soldierCount: 3, toRegionId: region.Id);

        _sut.ResolveAttack(match, army, region, general);

        Assert.Null(region.OwnerId);
        Assert.Equal(GeneralStatus.Dead, general.Status);
        Assert.NotNull(general.RespawnAtUtc);
        Assert.Equal(5 - 3, region.NeutralDefenseSoldiers);
    }

    [Fact]
    public void ResolveAttack_ArcherBonus_RepelsOtherwiseWinningAttack()
    {
        var attacker = new Player { Id = "attacker", Slot = 0, Name = "Alice" };
        var defender = new Player { Id = "defender", Slot = 1, Name = "Bob" };
        var match = CreateMatch(attacker, defender);
        // 0 asker + 1 okçu * 2.0 çarpan = 2 savunma gücü.
        var region = new Region
        {
            Id = "dudelange",
            OwnerId = defender.Id,
            Nest = new Nest { RegionId = "dudelange", OwnerId = defender.Id, Level = 2, GarrisonSoldiers = 0, GarrisonArchers = 1 }
        };
        var general = new General { Id = "g1", OwnerId = attacker.Id, Status = GeneralStatus.Moving };
        // Saldıran güç (2) savunan güçten (2) büyük DEĞİL -> saldırı püskürtülür (eşitlikte savunan kazanır).
        var army = CreateArmy(attacker.Id, general.Id, soldierCount: 2, toRegionId: region.Id);

        _sut.ResolveAttack(match, army, region, general);

        Assert.Equal(defender.Id, region.OwnerId);
        Assert.Equal(GeneralStatus.Dead, general.Status);
    }

    [Fact]
    public void ResolveAttack_CapturingLastEnemyNest_EliminatesDefender()
    {
        var attacker = new Player { Id = "attacker", Slot = 0, Name = "Alice" };
        var defender = new Player { Id = "defender", Slot = 1, Name = "Bob" };
        var match = CreateMatch(attacker, defender);
        var region = new Region
        {
            Id = "dudelange",
            OwnerId = defender.Id,
            Nest = new Nest { RegionId = "dudelange", OwnerId = defender.Id, Level = 1, GarrisonSoldiers = 1 }
        };
        match.Regions[region.Id] = region; // defender'ın tek bölgesi/yuvası

        var general = new General { Id = "g1", OwnerId = attacker.Id, Status = GeneralStatus.Moving };
        var army = CreateArmy(attacker.Id, general.Id, soldierCount: 5, toRegionId: region.Id);

        _sut.ResolveAttack(match, army, region, general);

        Assert.True(defender.IsEliminated);
        Assert.Equal(attacker.Id, region.OwnerId);
    }
}
