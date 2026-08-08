using api.Models;
using api.Models.Rooms;
using api.Services.GameEngine;
using api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace api.Tests;

public class CombatServiceTests
{
    private readonly CombatService _sut = new(TestEventLog.Writer(), NullLogger<CombatService>.Instance);

    private static Match CreateMatch(RoomType type = RoomType.Standard, params Player[] players)
    {
        var match = new Match
        {
            Id = "m1",
            Room = new Room
            {
                Id = "r1",
                Type = type,
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

    private static Army CreateArmy(string ownerId, int soldierCount, string toRegionId) => new()
    {
        Id = "a1",
        SequenceNo = 0,
        OwnerId = ownerId,
        SoldierCount = soldierCount,
        FromRegionId = "from",
        ToRegionId = toRegionId,
        DepartedAtUtc = DateTime.UtcNow,
        ArrivesAtUtc = DateTime.UtcNow
    };

    [Fact]
    public void ResolveAttack_AttackerStrongerThanDefender_CapturesRegionAndEliminatesDefender()
    {
        var attacker = new Player { Id = "attacker", Slot = 0, Name = "Alice" };
        var defender = new Player { Id = "defender", Slot = 1, Name = "Bob" };
        var match = CreateMatch(players: [attacker, defender]);
        var region = new Region { Id = "wiltz", OriginalOwnerId = defender.Id, OwnerId = defender.Id, SoldierCount = 3 };
        match.Regions[region.Id] = region;

        var army = CreateArmy(attacker.Id, soldierCount: 5, toRegionId: region.Id);

        var result = _sut.ResolveAttack(match, army, region, DateTime.UtcNow);

        Assert.True(result.Captured);
        Assert.Equal(attacker.Id, region.OwnerId);
        Assert.Equal(5 - 3, region.SoldierCount);
        Assert.True(defender.IsEliminated);
    }

    [Fact]
    public void ResolveAttack_AttackerWeakerThanDefender_IsRepelledAndDefenderLosesAttackerCount()
    {
        var attacker = new Player { Id = "attacker", Slot = 0, Name = "Alice" };
        var defender = new Player { Id = "defender", Slot = 1, Name = "Bob" };
        var match = CreateMatch(players: [attacker, defender]);

        var region = new Region { Id = "wiltz", OriginalOwnerId = defender.Id, OwnerId = defender.Id, SoldierCount = 5 };
        match.Regions[region.Id] = region;

        var army = CreateArmy(attacker.Id, soldierCount: 3, toRegionId: region.Id);

        var result = _sut.ResolveAttack(match, army, region, DateTime.UtcNow);

        Assert.False(result.Captured);
        Assert.Equal(defender.Id, region.OwnerId);
        Assert.Equal(5 - 3, region.SoldierCount);
        Assert.False(defender.IsEliminated);
    }

    [Fact]
    public void ResolveAttack_PlusTwoRule_SingleRegion_TwoSoldiersAgainstDefaultDefenseLeavesOneGarrison()
    {
        // Müşterinin örneği: 1 bölge almak için en az 2 asker (1'i savunmayı yenmek
        // için ölür, 1'i garrison olarak kalır) — varsayılan gri savunma = 1.
        var attacker = new Player { Id = "attacker", Slot = 0, Name = "Alice" };
        var match = CreateMatch(players: [attacker]);
        var region = new Region { Id = "grey1", OriginalOwnerId = null, OwnerId = null, SoldierCount = 1 };
        match.Regions[region.Id] = region;

        var army = CreateArmy(attacker.Id, soldierCount: 2, toRegionId: region.Id);
        var result = _sut.ResolveAttack(match, army, region, DateTime.UtcNow);

        Assert.True(result.Captured);
        Assert.Equal(attacker.Id, region.OwnerId);
        Assert.Equal(1, region.SoldierCount);
    }

    [Fact]
    public void ResolveAttack_PlusTwoRule_ChainOfTwoRegionsViaSequentialSingleHopSends_FourSoldiersTotalCapturesBoth()
    {
        // Müşterinin örneği: 2 bölge zincirleme almak için en az 4 asker. Sürükle-bırak
        // mekaniğinde (Bölüm 6/15) her gönderim tek hop'tur ve fazlası otomatik bir
        // sonraki bölgeye devam etmez — bu yüzden zincir, iki AYRI ResolveAttack
        // çağrısıyla (aradaki askerin ikinci bölgeye ayrıca gönderildiği varsayımıyla)
        // toplamda 4 askerle doğrulanır.
        var attacker = new Player { Id = "attacker", Slot = 0, Name = "Alice" };
        var match = CreateMatch(players: [attacker]);
        var region1 = new Region { Id = "grey1", OriginalOwnerId = null, OwnerId = null, SoldierCount = 1 };
        var region2 = new Region { Id = "grey2", OriginalOwnerId = null, OwnerId = null, SoldierCount = 1 };
        match.Regions[region1.Id] = region1;
        match.Regions[region2.Id] = region2;

        var army1 = CreateArmy(attacker.Id, soldierCount: 2, toRegionId: region1.Id);
        var result1 = _sut.ResolveAttack(match, army1, region1, DateTime.UtcNow);

        Assert.True(result1.Captured);
        Assert.Equal(1, region1.SoldierCount);

        var army2 = CreateArmy(attacker.Id, soldierCount: 2, toRegionId: region2.Id);
        var result2 = _sut.ResolveAttack(match, army2, region2, DateTime.UtcNow);

        Assert.True(result2.Captured);
        Assert.Equal(1, region2.SoldierCount);
    }

    [Fact]
    public void ResolveAttack_CapturingHomeRegion_EliminatesDefenderAndTheirOtherRegionsBecomeNeutral()
    {
        var attacker = new Player { Id = "attacker", Slot = 0, Name = "Alice" };
        var defender = new Player { Id = "defender", Slot = 1, Name = "Bob" };
        var match = CreateMatch(players: [attacker, defender]);

        var homeRegion = new Region { Id = "dudelange", OriginalOwnerId = defender.Id, OwnerId = defender.Id, SoldierCount = 1 };
        match.Regions[homeRegion.Id] = homeRegion;

        // Defender'ın başka bir oyuncudan ele geçirdiği bölge.
        var capturedRegion = new Region { Id = "remich", OriginalOwnerId = "third-player", OwnerId = defender.Id, SoldierCount = 7 };
        match.Regions[capturedRegion.Id] = capturedRegion;

        var strandedArmy = new Army
        {
            Id = "stranded-army",
            SequenceNo = 1,
            OwnerId = defender.Id,
            SoldierCount = 2,
            FromRegionId = "x",
            ToRegionId = "y",
            DepartedAtUtc = DateTime.UtcNow,
            ArrivesAtUtc = DateTime.UtcNow.AddSeconds(5)
        };
        match.Armies.Add(strandedArmy);

        var army = CreateArmy(attacker.Id, soldierCount: 5, toRegionId: homeRegion.Id);
        var result = _sut.ResolveAttack(match, army, homeRegion, DateTime.UtcNow);

        Assert.True(result.Captured);
        Assert.True(defender.IsEliminated);
        Assert.Equal(attacker.Id, homeRegion.OwnerId);
        Assert.Null(capturedRegion.OwnerId);
        Assert.Equal(1, capturedRegion.SoldierCount); // Room.GreyRegionDefenseCount'a sıfırlanır (bkz. 03-game-rules.md Bölüm 8).
        Assert.DoesNotContain(match.Armies, a => a.OwnerId == defender.Id);
    }
}
