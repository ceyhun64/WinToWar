using api.Models;
using api.Models.Rooms;
using api.Services;
using api.Services.GameEngine;
using api.Services.Matchmaking;
using api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace api.Tests;

/// <summary>
/// docs/03-game-rules.md Bölüm 7 (DÜZELTME — "bot yok" kararı geri alındı):
/// lobi bot-doldurma (matchmaking) ve bot AI saldırı kararı testleri.
/// </summary>
public class BotMatchServiceTests
{
    private readonly MapProvider _mapProvider;
    private readonly MatchManager _matchManager;
    private readonly BotMatchService _sut;

    public BotMatchServiceTests()
    {
        _mapProvider = new MapProvider(new FakeHostEnvironment(), NullLogger<MapProvider>.Instance);
        _matchManager = new MatchManager(_mapProvider, TestEventLog.Writer(), NullLogger<MatchManager>.Instance);
        var combatService = new CombatService(TestEventLog.Writer(), NullLogger<CombatService>.Instance);
        var movementService = new MovementService(_mapProvider, combatService, NullLogger<MovementService>.Instance);
        _sut = new BotMatchService(_matchManager, _mapProvider, movementService, NullLogger<BotMatchService>.Instance);
    }

    private Match CreateStandardMatch(int maxPlayers = 4) =>
        _matchManager.CreateMatch(new Room
        {
            Id = "r1",
            Type = RoomType.Standard,
            MaxPlayers = maxPlayers,
            GreyRegionDefenseCount = 1,
            FogOfWar = false,
            EntryFeeUsd = 1.00m,
            CreatorPlayerId = string.Empty
        }, DateTime.UtcNow);

    private Match CreateVipMatch(int maxPlayers = 4) =>
        _matchManager.CreateMatch(new Room
        {
            Id = "r-vip",
            Type = RoomType.Vip,
            MaxPlayers = maxPlayers,
            GreyRegionDefenseCount = 1,
            FogOfWar = false,
            EntryFeeUsd = 1.00m,
            CreatorPlayerId = "creator"
        }, DateTime.UtcNow);

    [Fact]
    public void ReservePlayer_FirstHumanJoinsStandardRoom_SetsBotFillDeadlineWithinConfiguredRange()
    {
        var match = CreateStandardMatch();
        var now = DateTime.UtcNow;

        _matchManager.ReservePlayer(match, "Alice", now);

        Assert.NotNull(match.BotFillDeadlineUtc);
        var waitSeconds = (match.BotFillDeadlineUtc!.Value - now).TotalSeconds;
        Assert.InRange(waitSeconds, GameConfig.BotMatchWaitMinSeconds, GameConfig.BotMatchWaitMaxSeconds);
    }

    [Fact]
    public void ReservePlayer_FirstOccupantJoinsVipRoom_DoesNotSetBotFillDeadline()
    {
        var match = CreateVipMatch();

        _matchManager.ReservePlayer(match, "Creator", DateTime.UtcNow, forcedPlayerId: "creator");

        Assert.Null(match.BotFillDeadlineUtc);
    }

    [Fact]
    public void ReservePlayer_SecondHumanJoins_DoesNotResetBotFillDeadline()
    {
        var match = CreateStandardMatch();
        var now = DateTime.UtcNow;
        _matchManager.ReservePlayer(match, "Alice", now);
        var firstDeadline = match.BotFillDeadlineUtc;

        _matchManager.ReservePlayer(match, "Bob", now.AddSeconds(3));

        Assert.Equal(firstDeadline, match.BotFillDeadlineUtc);
    }

    [Fact]
    public void FillLobbyWithBots_StandardRoomNotFull_FillsAllRemainingSlotsAndStartsCountdown()
    {
        var match = CreateStandardMatch(maxPlayers: 4);
        var now = DateTime.UtcNow;
        _matchManager.ReservePlayer(match, "Alice", now);
        _matchManager.ConfirmPlayerPayment(match.Id, match.Players[0].Id, now);

        var added = _sut.FillLobbyWithBots(match, now.AddSeconds(15));

        Assert.Equal(3, added.Count);
        Assert.All(added, p => Assert.True(p.IsBot));
        Assert.All(added, p => Assert.True(p.IsPaymentConfirmed));
        Assert.All(added, p => Assert.NotNull(p.BotDifficulty));
        Assert.Equal(4, match.Players.Count);
        Assert.Equal(MatchStatus.Countdown, match.Status);
    }

    [Fact]
    public void FillLobbyWithBots_VipRoom_NeverAddsBots()
    {
        var match = CreateVipMatch(maxPlayers: 4);
        _matchManager.ReservePlayer(match, "Creator", DateTime.UtcNow, forcedPlayerId: "creator");

        var added = _sut.FillLobbyWithBots(match, DateTime.UtcNow);

        Assert.Empty(added);
        Assert.Single(match.Players);
        Assert.Equal(MatchStatus.Lobby, match.Status);
    }

    [Fact]
    public void FillLobbyWithBots_RoomAlreadyFull_ReturnsEmpty()
    {
        var match = CreateStandardMatch(maxPlayers: 1);
        var now = DateTime.UtcNow;
        _matchManager.ReservePlayer(match, "Alice", now);
        _matchManager.ConfirmPlayerPayment(match.Id, match.Players[0].Id, now);

        var added = _sut.FillLobbyWithBots(match, now);

        Assert.Empty(added);
    }

    private static Match CreatePlayingMatchWithBot(BotDifficulty difficulty, int homeSoldierCount, int neighborSoldierCount)
    {
        var owner = new Player { Id = "bot1", Slot = 0, Name = "Bot 1", IsBot = true, BotDifficulty = difficulty };
        var match = new Match
        {
            Id = "m1",
            Room = new Room
            {
                Id = "r1",
                Type = RoomType.Standard,
                MaxPlayers = 2,
                GreyRegionDefenseCount = 1,
                FogOfWar = false,
                EntryFeeUsd = 1.00m,
                CreatorPlayerId = string.Empty
            },
            Status = MatchStatus.Playing,
            StartedAtUtc = DateTime.UtcNow
        };
        match.Players.Add(owner);
        match.Regions["luxembourg-city"] = new Region
        {
            Id = "luxembourg-city", OriginalOwnerId = owner.Id, OwnerId = owner.Id, SoldierCount = homeSoldierCount
        };
        // Komşular (luxembourg-city'nin üçü de): esch-sur-alzette hedef (değişken asker
        // sayısı ile test senaryosuna göre zayıf/güçlü), steinfort ve ettelbruck her
        // zaman çok güçlü (999) — hiçbir testte yanlışlıkla "en zayıf" seçilmesinler diye.
        match.Regions["esch-sur-alzette"] = new Region
        {
            Id = "esch-sur-alzette", OriginalOwnerId = null, OwnerId = "enemy", SoldierCount = neighborSoldierCount
        };
        match.Regions["steinfort"] = new Region
        {
            Id = "steinfort", OriginalOwnerId = null, OwnerId = "enemy", SoldierCount = 999
        };
        match.Regions["ettelbruck"] = new Region
        {
            Id = "ettelbruck", OriginalOwnerId = null, OwnerId = "enemy", SoldierCount = 999
        };
        return match;
    }

    [Fact]
    public void ProcessBotDecisions_NormalBotWithCapturableWeakestNeighbor_AttacksIt()
    {
        var match = CreatePlayingMatchWithBot(BotDifficulty.Normal, homeSoldierCount: 5, neighborSoldierCount: 2);
        var bot = match.Players[0];
        bot.LastActionAtUtc = DateTime.UtcNow.AddSeconds(-GameConfig.BotDecisionIntervalSecondsNormal - 1);

        _sut.ProcessBotDecisions(match, DateTime.UtcNow);

        // docs/19-army.md: bot da StartDispatch üzerinden aynı kademeli sevkiyat
        // mekanizmasını kullanır — sendable=5'in TAMAMI anında tek bir Army olarak
        // değil, bir Dispatch olarak rezerve edilir, ilk grup anında yola çıkar.
        var dispatch = Assert.Single(match.Dispatches);
        Assert.Equal("luxembourg-city", dispatch.FromRegionId);
        Assert.Equal("esch-sur-alzette", dispatch.ToRegionId);
        Assert.Equal(5 - GameConfig.MinGarrisonPerSend, dispatch.TotalAmount);

        var army = Assert.Single(match.Armies);
        Assert.Equal("luxembourg-city", army.FromRegionId);
        Assert.Equal("esch-sur-alzette", army.ToRegionId);
        Assert.InRange(army.SoldierCount, 1, dispatch.TotalAmount);
    }

    [Fact]
    public void ProcessBotDecisions_NoCapturableNeighbor_DoesNotAttack()
    {
        // Ev bölgesindeki asker çok az — hiçbir komşuyu (esch-sur-alzette=2 dahil) fethedemez.
        var match = CreatePlayingMatchWithBot(BotDifficulty.Normal, homeSoldierCount: 2, neighborSoldierCount: 5);
        var bot = match.Players[0];
        bot.LastActionAtUtc = DateTime.UtcNow.AddSeconds(-GameConfig.BotDecisionIntervalSecondsNormal - 1);

        _sut.ProcessBotDecisions(match, DateTime.UtcNow);

        Assert.Empty(match.Armies);
    }

    [Fact]
    public void ProcessBotDecisions_DecisionIntervalNotElapsed_DoesNotAttackYet()
    {
        var match = CreatePlayingMatchWithBot(BotDifficulty.Normal, homeSoldierCount: 5, neighborSoldierCount: 2);
        var bot = match.Players[0];
        bot.LastActionAtUtc = DateTime.UtcNow; // Az önce karar verdi.

        _sut.ProcessBotDecisions(match, DateTime.UtcNow.AddSeconds(1));

        Assert.Empty(match.Armies);
    }

    [Fact]
    public void ProcessBotDecisions_EasyBotWithSmallSurplus_StaysPassive()
    {
        // sendable = 3 - 1 = 2, BotEasyMinSurplusToAttack(6)'nın altında — Kolay bot saldırmaz.
        var match = CreatePlayingMatchWithBot(BotDifficulty.Easy, homeSoldierCount: 3, neighborSoldierCount: 1);
        var bot = match.Players[0];
        bot.LastActionAtUtc = DateTime.UtcNow.AddSeconds(-GameConfig.BotDecisionIntervalSecondsEasy - 1);

        _sut.ProcessBotDecisions(match, DateTime.UtcNow);

        Assert.Empty(match.Armies);
    }

    [Fact]
    public void ProcessBotDecisions_EliminatedBot_NeverActs()
    {
        var match = CreatePlayingMatchWithBot(BotDifficulty.Hard, homeSoldierCount: 5, neighborSoldierCount: 2);
        var bot = match.Players[0];
        bot.IsEliminated = true;
        bot.LastActionAtUtc = DateTime.UtcNow.AddSeconds(-GameConfig.BotDecisionIntervalSecondsHard - 1);

        _sut.ProcessBotDecisions(match, DateTime.UtcNow);

        Assert.Empty(match.Armies);
    }
}
