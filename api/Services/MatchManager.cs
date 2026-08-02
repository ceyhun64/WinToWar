using System.Collections.Concurrent;
using api.Models;

namespace api.Services;

/// <summary>
/// Aktif maçları ve SignalR bağlantı-oyuncu eşlemesini bellekte tutan tek
/// doğruluk kaynağı. Match başına state mutasyonları çağıran taraf (GameHub,
/// EconomyTickService) Match.Lock içinde çalışmalıdır (bkz. Bölüm 6.1 thread safety).
/// </summary>
public class MatchManager
{
    private readonly MapProvider _mapProvider;
    private readonly ILogger<MatchManager> _logger;
    private readonly ConcurrentDictionary<string, Match> _matches = new();
    private readonly ConcurrentDictionary<string, (string MatchId, string PlayerId)> _connections = new();

    public MatchManager(MapProvider mapProvider, ILogger<MatchManager> logger)
    {
        _mapProvider = mapProvider;
        _logger = logger;
    }

    public IReadOnlyCollection<Match> ActiveMatches => (IReadOnlyCollection<Match>)_matches.Values;

    public Match CreateMatch()
    {
        var match = new Match { Id = Guid.NewGuid().ToString("N") };
        _matches[match.Id] = match;
        _logger.LogInformation("Yeni maç oluşturuldu: {MatchId}", match.Id);
        return match;
    }

    public bool TryGetMatch(string matchId, out Match match)
    {
        return _matches.TryGetValue(matchId, out match!);
    }

    /// <summary>
    /// REST üzerinden bir oyuncuyu maça kaydeder (henüz canlı SignalR bağlantısı yok).
    /// Bağlantı, client SignalR ile bağlanıp <see cref="ReconnectPlayer"/> çağırdığında eşlenir.
    /// </summary>
    public Player AddPlayer(Match match, string playerName)
    {
        lock (match.Lock)
        {
            if (match.Players.Count >= GameConfig.DefaultPlayerCount)
            {
                throw new InvalidOperationException("Maç dolu.");
            }

            var slot = match.Players.Count;
            var player = new Player
            {
                Id = Guid.NewGuid().ToString("N"),
                Slot = slot,
                Name = playerName,
                ConnectionId = null,
                ConnectionStatus = PlayerConnectionStatus.Disconnected
            };
            match.Players.Add(player);

            if (match.Players.Count == GameConfig.DefaultPlayerCount)
            {
                StartMatch(match);
            }

            return player;
        }
    }

    /// <summary>Match.Lock altında çağrılmalıdır.</summary>
    private void StartMatch(Match match)
    {
        foreach (var regionDef in _mapProvider.Map.Regions)
        {
            var region = new Region { Id = regionDef.Id };

            if (regionDef.IsStartingRegion && regionDef.StartingPlayerSlot is int slot)
            {
                var owner = match.Players.First(p => p.Slot == slot);
                region.OwnerId = owner.Id;
                region.Nest = new Nest
                {
                    RegionId = region.Id,
                    OwnerId = owner.Id,
                    Level = 1
                };

                var general = new General
                {
                    Id = Guid.NewGuid().ToString("N"),
                    OwnerId = owner.Id,
                    Status = GeneralStatus.Garrisoned,
                    CurrentRegionId = region.Id
                };
                match.Generals.Add(general);
            }
            else
            {
                region.NeutralDefenseSoldiers = GameConfig.NeutralRegionDefenseSoldiers;
            }

            match.Regions[region.Id] = region;
        }

        match.Status = MatchStatus.InProgress;
        match.StartedAtUtc = DateTime.UtcNow;
        _logger.LogInformation("Maç başladı: {MatchId}", match.Id);
    }

    public bool TryGetByConnection(string connectionId, out string matchId, out string playerId)
    {
        if (_connections.TryGetValue(connectionId, out var info))
        {
            matchId = info.MatchId;
            playerId = info.PlayerId;
            return true;
        }

        matchId = string.Empty;
        playerId = string.Empty;
        return false;
    }

    public void MarkDisconnected(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var info))
        {
            return;
        }

        if (_matches.TryGetValue(info.MatchId, out var match))
        {
            lock (match.Lock)
            {
                var player = match.Players.FirstOrDefault(p => p.Id == info.PlayerId);
                if (player is not null)
                {
                    player.ConnectionStatus = PlayerConnectionStatus.Disconnected;
                    player.ConnectionId = null;
                }
            }
        }
    }

    public void ReconnectPlayer(Match match, string playerId, string connectionId)
    {
        lock (match.Lock)
        {
            var player = match.Players.FirstOrDefault(p => p.Id == playerId)
                ?? throw new InvalidOperationException("Oyuncu bu maçta bulunamadı.");
            player.ConnectionId = connectionId;
            player.ConnectionStatus = PlayerConnectionStatus.Connected;
            _connections[connectionId] = (match.Id, playerId);
        }
    }
}
