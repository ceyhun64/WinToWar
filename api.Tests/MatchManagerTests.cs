using api.Models;
using api.Models.Rooms;
using api.Services;
using api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace api.Tests;

/// <summary>
/// docs/05-payment.md Bölüm 11 (Kabul Kriterleri): "Oyuncu Match.Status = Lobby
/// iken LeaveLobby çağırdığında tam otomatik refund tetikleniyor; Countdown/Playing
/// durumunda bu aksiyon reddediliyor." GameHub.LeaveLobby bu davranışı doğrudan
/// MatchManager.RemovePlayerFromLobby'nin dönüş değerine dayandırır (bkz. GameHub.cs) —
/// refund'un kendisi yalnızca bu true döndüğünde tetiklenir.
/// </summary>
public class MatchManagerTests
{
    private readonly MatchManager _sut = new(new MapProvider(new FakeHostEnvironment(), NullLogger<MapProvider>.Instance), TestEventLog.Writer(), NullLogger<MatchManager>.Instance);

    private static Room CreateRoom(int maxPlayers = 2) => new()
    {
        Id = "r1",
        Type = RoomType.Vip,
        MaxPlayers = maxPlayers,
        GreyRegionDefenseCount = 1,
        FogOfWar = false,
        EntryFeeUsd = 1.00m,
        CreatorPlayerId = "creator"
    };

    [Fact]
    public void RemovePlayerFromLobby_MatchStillInLobby_RemovesPlayerAndReturnsTrue()
    {
        var match = _sut.CreateMatch(CreateRoom(), DateTime.UtcNow);
        var player = _sut.ReservePlayer(match, "Alice");

        var removed = _sut.RemovePlayerFromLobby(match, player.Id);

        Assert.True(removed);
        Assert.DoesNotContain(match.Players, p => p.Id == player.Id);
    }

    [Fact]
    public void RemovePlayerFromLobby_MatchInCountdown_ReturnsFalseAndDoesNotRemove()
    {
        var match = _sut.CreateMatch(CreateRoom(), DateTime.UtcNow);
        var player = _sut.ReservePlayer(match, "Alice");
        match.Status = MatchStatus.Countdown;

        var removed = _sut.RemovePlayerFromLobby(match, player.Id);

        Assert.False(removed);
        Assert.Contains(match.Players, p => p.Id == player.Id);
    }

    [Fact]
    public void RemovePlayerFromLobby_MatchPlaying_ReturnsFalseAndDoesNotRemove()
    {
        var match = _sut.CreateMatch(CreateRoom(), DateTime.UtcNow);
        var player = _sut.ReservePlayer(match, "Alice");
        match.Status = MatchStatus.Playing;

        var removed = _sut.RemovePlayerFromLobby(match, player.Id);

        Assert.False(removed);
        Assert.Contains(match.Players, p => p.Id == player.Id);
    }

    [Fact]
    public void RemovePlayerFromLobby_LastPlayerLeaves_CancelsLobby()
    {
        var match = _sut.CreateMatch(CreateRoom(), DateTime.UtcNow);
        var player = _sut.ReservePlayer(match, "Alice");

        _sut.RemovePlayerFromLobby(match, player.Id);

        Assert.Equal(MatchStatus.Cancelled, match.Status);
    }

    [Fact]
    public void RemovePlayerFromLobby_OtherPlayersRemain_LobbyStaysOpen()
    {
        var match = _sut.CreateMatch(CreateRoom(maxPlayers: 3), DateTime.UtcNow);
        var alice = _sut.ReservePlayer(match, "Alice");
        _sut.ReservePlayer(match, "Bob");

        _sut.RemovePlayerFromLobby(match, alice.Id);

        Assert.Equal(MatchStatus.Lobby, match.Status);
        Assert.Single(match.Players);
    }
}
