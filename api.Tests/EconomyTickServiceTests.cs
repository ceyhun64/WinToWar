using api;
using api.Hubs;
using api.Models;
using api.Models.Rooms;
using api.Services;
using api.Services.GameEngine;
using api.Services.Matchmaking;
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
        var botMatchService = new BotMatchService(matchManager, mapProvider, movementService, NullLogger<BotMatchService>.Instance);
        _sut = new EconomyTickService(
            matchManager, movementService, botMatchService, eventLogWriter,
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
    public void Tick_OneIntervalElapsed_ProducesExactlyOneInterval()
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

    /// <summary>docs/03-game-rules.md Bölüm 4: aynı zaman damgasıyla (0 saniye geçmiş) tekrar tick atmak üretimi tekrarlamaz.</summary>
    [Fact]
    public void Tick_NoTimeElapsed_DoesNotProduceYet()
    {
        var player = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var start = DateTime.UtcNow;
        var match = CreateMatch(start, player);
        var home = new Region { Id = "home", OriginalOwnerId = player.Id, OwnerId = player.Id, SoldierCount = 0 };
        match.Regions[home.Id] = home;

        _sut.Tick(match, start);

        Assert.Equal(0, home.SoldierCount);
    }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 4 (müşteri kararıyla güncellendi): fethedilen bölgeler
    /// artık Ana Kale'ye dolaylı bir bonus eklemez — kendi askerini kendi üretir, Ana Kale
    /// ile birebir aynı oranda. "1'de kalma" hatası burada test ediliyor: bir tam interval
    /// sonra fethedilmiş bölgeler de tıpkı Ana Kale gibi BaseProductionPerInterval kadar büyümüş olmalı.
    /// </summary>
    [Fact]
    public void Tick_ConqueredRegionsProduceTheirOwnSoldiersJustLikeHome()
    {
        var player = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var start = DateTime.UtcNow;
        var match = CreateMatch(start, player);
        var home = new Region { Id = "home", OriginalOwnerId = player.Id, OwnerId = player.Id, SoldierCount = 0 };
        var conquered1 = new Region { Id = "c1", OriginalOwnerId = null, OwnerId = player.Id, SoldierCount = 1 };
        var conquered2 = new Region { Id = "c2", OriginalOwnerId = null, OwnerId = player.Id, SoldierCount = 1 };
        match.Regions[home.Id] = home;
        match.Regions[conquered1.Id] = conquered1;
        match.Regions[conquered2.Id] = conquered2;

        _sut.Tick(match, start.AddSeconds(GameConfig.ProductionIntervalSeconds));

        Assert.Equal(GameConfig.BaseProductionPerInterval, home.SoldierCount);
        Assert.Equal(1 + GameConfig.BaseProductionPerInterval, conquered1.SoldierCount);
        Assert.Equal(1 + GameConfig.BaseProductionPerInterval, conquered2.SoldierCount);
    }

    [Fact]
    public void Tick_RegionProductionRespectsPerRegionCap()
    {
        var player = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var start = DateTime.UtcNow;
        var match = CreateMatch(start, player);
        var home = new Region { Id = "home", OriginalOwnerId = player.Id, OwnerId = player.Id, SoldierCount = 0 };
        var nearCap = new Region
        {
            Id = "c1",
            OriginalOwnerId = null,
            OwnerId = player.Id,
            SoldierCount = GameConfig.MaxAccumulatedTroops - 1
        };
        match.Regions[home.Id] = home;
        match.Regions[nearCap.Id] = nearCap;

        _sut.Tick(match, start.AddSeconds(GameConfig.ProductionIntervalSeconds));

        Assert.Equal(GameConfig.MaxAccumulatedTroops, nearCap.SoldierCount);
    }

    private static Match CreateMatchWithGreyRegionDefense(int greyRegionDefenseCount) => new()
    {
        Id = "m1",
        Status = MatchStatus.Playing,
        StartedAtUtc = DateTime.UtcNow,
        Room = new Room
        {
            Id = "r1",
            Type = RoomType.Standard,
            MaxPlayers = 4,
            GreyRegionDefenseCount = greyRegionDefenseCount,
            FogOfWar = false,
            EntryFeeUsd = 1.00m,
            CreatorPlayerId = "creator"
        }
    };

    /// <summary>
    /// docs/03-game-rules.md Bölüm 4 (yeni müşteri talimatı): fethedilmeyen bir bölge
    /// saldırıyla zayıflatılıp (ör. 10 savunmaya 6 asker gönderilip püskürtülürse 4'e
    /// düşer) ele geçirilemezse, o andan itibaren her saniye +1 kendiliğinden iyileşir.
    /// </summary>
    /// <summary>
    /// GameConfig.GameTickMs artık 1 saniyeden kısa (kullanıcı talimatı — sevkiyat
    /// gruplarının görünür "adım" süresini azaltmak için düşürüldü), bu yüzden nötr
    /// iyileşme de (tıpkı üretim gibi) elapsed-time interval sayımıyla çalışır — bir
    /// tam saniye geçmeden regen uygulanmaz.
    /// </summary>
    [Fact]
    public void Tick_NeutralRegionBelowCap_RegeneratesOneSoldierPerSecond()
    {
        var match = CreateMatchWithGreyRegionDefense(greyRegionDefenseCount: 10);
        var start = match.StartedAtUtc!.Value;
        var neutral = new Region { Id = "n1", OriginalOwnerId = null, OwnerId = null, SoldierCount = 4 };
        match.Regions[neutral.Id] = neutral;

        _sut.Tick(match, start.AddSeconds(GameConfig.NeutralRegenIntervalSeconds));

        Assert.Equal(5, neutral.SoldierCount);
    }

    [Fact]
    public void Tick_NeutralRegionNoTimeElapsed_DoesNotRegenerateYet()
    {
        var match = CreateMatchWithGreyRegionDefense(greyRegionDefenseCount: 10);
        var start = match.StartedAtUtc!.Value;
        var neutral = new Region { Id = "n1", OriginalOwnerId = null, OwnerId = null, SoldierCount = 4 };
        match.Regions[neutral.Id] = neutral;

        _sut.Tick(match, start);

        Assert.Equal(4, neutral.SoldierCount);
    }

    [Fact]
    public void Tick_NeutralRegionAtCap_DoesNotExceedCap()
    {
        var match = CreateMatchWithGreyRegionDefense(greyRegionDefenseCount: 10);
        var start = match.StartedAtUtc!.Value;
        var neutral = new Region { Id = "n1", OriginalOwnerId = null, OwnerId = null, SoldierCount = 10 };
        match.Regions[neutral.Id] = neutral;

        _sut.Tick(match, start.AddSeconds(GameConfig.NeutralRegenIntervalSeconds));

        Assert.Equal(10, neutral.SoldierCount);
    }

    [Fact]
    public void Tick_RegionOwnedByUnknownPlayerId_DoesNotProduce()
    {
        var player = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var start = DateTime.UtcNow;
        var match = CreateMatch(start, player);
        // "someone-else" match.Players içinde kayıtlı değil (ör. elenmiş/kayıp bir
        // referans) -> bu bölge kimse için üretmez, sessizce atlanır.
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
