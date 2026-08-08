using System.Collections.Concurrent;
using api.Models;
using api.Models.Rooms;

namespace api.Services;

/// <summary>
/// Aktif maçları ve SignalR bağlantı-oyuncu eşlemesini bellekte tutan tek
/// doğruluk kaynağı. Match başına state mutasyonları çağıran taraf (GameHub,
/// EconomyTickService, RoomService) Match.Lock içinde çalışmalıdır.
///
/// Lobi modeli (docs/03-game-rules.md): bir maç, oda ayarındaki oyuncu sayısı
/// (Room.MaxPlayers — Standart'ta sabit 4, VIP'de 2-12, Practice'te varsayılan 2)
/// dolana kadar başlamaz. Bir oyuncu <see cref="ReservePlayer"/> ile bir slot ayırtır
/// (henüz ödeme onaylanmamış, Practice hariç); yalnızca <see cref="ConfirmPlayerPayment"/>
/// çağrıldığında (PaymentConfirmed event'i) o oyuncu "dolu" sayılır.
/// </summary>
public class MatchManager
{
    private readonly MapProvider _mapProvider;
    private readonly MatchEventLogWriter _eventLogWriter;
    private readonly ILogger<MatchManager> _logger;
    private readonly ConcurrentDictionary<string, Match> _matches = new();
    private readonly ConcurrentDictionary<string, (string MatchId, string PlayerId)> _connections = new();

    public MatchManager(MapProvider mapProvider, MatchEventLogWriter eventLogWriter, ILogger<MatchManager> logger)
    {
        _mapProvider = mapProvider;
        _eventLogWriter = eventLogWriter;
        _logger = logger;
    }

    public IReadOnlyCollection<Match> ActiveMatches => (IReadOnlyCollection<Match>)_matches.Values;

    public Match CreateMatch(Room room, DateTime now)
    {
        var match = new Match { Id = Guid.NewGuid().ToString("N"), Room = room, LobbyOpenedAtUtc = now };
        _matches[match.Id] = match;
        _logger.LogInformation("Yeni lobi oluşturuldu: {MatchId}, tür {RoomType}, kapasite {MaxPlayers}",
            match.Id, room.Type, room.MaxPlayers);
        return match;
    }

    public bool TryGetMatch(string matchId, out Match match)
    {
        return _matches.TryGetValue(matchId, out match!);
    }

    /// <summary>Aynı türde, henüz dolmamış açık bir lobi ararken kullanılır (Standart/Practice tek kuyruk).</summary>
    public Match? FindJoinableRoom(RoomType type)
        => _matches.Values.FirstOrDefault(m =>
            m.Room.Type == type && m.Status == MatchStatus.Lobby && m.Players.Count < m.Room.MaxPlayers);

    /// <summary>
    /// Bir oyuncu için lobide bir slot ayırtır. VIP kurucusu için forcedPlayerId
    /// kullanılır ki RoomService'in Room.CreatorPlayerId'siyle aynı Id kullanılsın.
    /// docs/03-game-rules.md Bölüm 7 (DÜZELTME): Standart/Practice lobisine giren
    /// İLK gerçek (bot olmayan) oyuncu, GameConfig.BotMatchWaitMinSeconds-MaxSeconds
    /// arası rastgele bir bot-doldurma zaman aşımı başlatır (VIP'de asla — kurucu
    /// zaten manuel/"Şimdi Başlat" kontrolüne sahip, bkz. BotMatchService).
    /// </summary>
    public Player ReservePlayer(
        Match match, string playerName, DateTime now, string? forcedPlayerId = null, string? joinIpAddress = null,
        bool isBot = false, BotDifficulty? botDifficulty = null)
    {
        lock (match.Lock)
        {
            if (match.Status != MatchStatus.Lobby)
            {
                throw new InvalidOperationException("Lobi artık yeni oyuncu kabul etmiyor.");
            }

            if (match.Players.Count >= match.Room.MaxPlayers)
            {
                throw new InvalidOperationException("Lobi dolu.");
            }

            if (!isBot && match.Players.Count == 0 && match.Room.Type != RoomType.Vip)
            {
                match.BotFillDeadlineUtc = now.AddSeconds(
                    Random.Shared.Next(GameConfig.BotMatchWaitMinSeconds, GameConfig.BotMatchWaitMaxSeconds + 1));
            }

            var player = new Player
            {
                Id = forcedPlayerId ?? Guid.NewGuid().ToString("N"),
                Slot = match.Players.Count,
                Name = playerName,
                ConnectionId = null,
                ConnectionStatus = PlayerConnectionStatus.Disconnected,
                JoinIpAddress = joinIpAddress,
                IsBot = isBot,
                BotDifficulty = botDifficulty
            };

            if (joinIpAddress is not null && match.Players.Any(p => p.JoinIpAddress == joinIpAddress))
            {
                _logger.LogWarning(
                    "SuspiciousActivityLog: aynı IP'den ({IpAddress}) aynı odaya ({MatchId}) 2+ katılım tespit edildi — multi-accounting/self-play riski, otomatik engellenmedi.",
                    joinIpAddress, match.Id);
            }

            match.Players.Add(player);
            return player;
        }
    }

    /// <summary>
    /// Ödeme modülünden (PaymentConfirmed) veya Practice katılımında doğrudan çağrılır:
    /// oyuncuyu "dolu" olarak işaretler. Oda kapasitesine ulaşıldığında Countdown'a geçer.
    /// </summary>
    public void ConfirmPlayerPayment(string matchId, string playerId, DateTime now)
    {
        if (!TryGetMatch(matchId, out var match))
        {
            return;
        }

        lock (match.Lock)
        {
            var player = match.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is null || match.Status != MatchStatus.Lobby)
            {
                return;
            }

            player.IsPaymentConfirmed = true;
            _logger.LogInformation("Oyuncu ödemesi onaylandı: {PlayerId}, lobi {MatchId} ({Confirmed}/{Total})",
                playerId, matchId, match.Players.Count(p => p.IsPaymentConfirmed), match.Room.MaxPlayers);

            if (match.Players.Count == match.Room.MaxPlayers && match.Players.All(p => p.IsPaymentConfirmed))
            {
                match.Status = MatchStatus.Countdown;
                match.CountdownEndsAtUtc = now.AddSeconds(GameConfig.LobbyCountdownSeconds);
                _logger.LogInformation("Lobi doldu, geri sayım başladı: {MatchId}", match.Id);
            }
        }
    }

    /// <summary>
    /// Bir oyuncu, lobi henüz dolmadan (Countdown başlamadan) kendi isteğiyle
    /// ayrılabilir (docs/05-payment.md Bölüm 1.7) — çağıran taraf (GameHub) bunun
    /// ardından oyuncunun PaymentInvoice'ı için refund tetiklemelidir. Herkes
    /// ayrılırsa lobi tamamen kapatılır (docs/03-game-rules.md Bölüm 10).
    /// </summary>
    public bool RemovePlayerFromLobby(Match match, string playerId)
    {
        lock (match.Lock)
        {
            if (match.Status != MatchStatus.Lobby)
            {
                return false;
            }

            var player = match.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is null)
            {
                return false;
            }

            match.Players.Remove(player);
            _logger.LogInformation("Oyuncu lobiden ayrıldı: {PlayerId}, lobi {MatchId}", playerId, match.Id);

            if (match.Players.Count == 0)
            {
                match.Status = MatchStatus.Cancelled;
                _logger.LogInformation("Lobi tamamen boşaldı, kapatıldı: {MatchId}", match.Id);
            }

            return true;
        }
    }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 8 "VIP masa manuel başlatma": VIP masa kurucusu,
    /// tüm koltuklar dolmadan mevcut oyuncu sayısıyla (CurrentPlayerCount &gt;=
    /// VipRoomMinPlayers) maçı başlatabilir. Standart/Practice odalarda kurucu
    /// kavramı yok, bu aksiyon yalnızca VIP için geçerlidir. Countdown'a geçirir —
    /// asıl harita kurulumu StartPlaying ile (EconomyTickService tick'inde,
    /// Countdown süresi dolunca) aynı şekilde yürür, ek bir kod yolu gerekmez.
    /// </summary>
    public bool TryForceStartVip(Match match, string requesterId, DateTime now)
    {
        lock (match.Lock)
        {
            if (match.Room.Type != RoomType.Vip || match.Room.CreatorPlayerId != requesterId)
            {
                return false;
            }

            if (match.Status != MatchStatus.Lobby)
            {
                return false;
            }

            var confirmedCount = match.Players.Count(p => p.IsPaymentConfirmed);
            if (confirmedCount < GameConfig.VipRoomMinPlayers)
            {
                return false;
            }

            // Ödemesi hâlâ onaylanmamış (ör. top-up bekleyen) rezerve slotlar varsa
            // bu oyuncular listeden çıkarılır — StartPlaying zaten yalnızca ödemesi
            // onaylı oyunculara bölge atar (Room.MaxPlayers, VIP kurucunun seçtiği
            // orijinal kapasite olarak değişmeden kalır; Bölüm 2.2'deki "N < harita
            // bölge sayısı ise kalan bölgeler nötr başlar" mekanizmasıyla aynıdır).
            match.Players.RemoveAll(p => !p.IsPaymentConfirmed);

            match.Status = MatchStatus.Countdown;
            match.CountdownEndsAtUtc = now.AddSeconds(GameConfig.LobbyCountdownSeconds);
            _logger.LogInformation("VIP masa kurucu tarafından erken başlatıldı: {MatchId}, oyuncu sayısı {PlayerCount}", match.Id, match.Players.Count);
            return true;
        }
    }

    /// <summary>Match.Lock altında çağrılmalıdır: Countdown süresi dolduğunda haritayı kurup maçı başlatır.</summary>
    public void StartPlaying(Match match, DateTime now)
    {
        var confirmedPlayers = match.Players.Where(p => p.IsPaymentConfirmed).ToList();
        var homeRegionIds = _mapProvider.PickRandomStartingRegionIds(confirmedPlayers.Count);

        for (var i = 0; i < confirmedPlayers.Count; i++)
        {
            var player = confirmedPlayers[i];
            match.Regions[homeRegionIds[i]] = new Region
            {
                Id = homeRegionIds[i],
                OriginalOwnerId = player.Id,
                OwnerId = player.Id,
                SoldierCount = GameConfig.StartingSoldierCount
            };
        }

        foreach (var regionDef in _mapProvider.Map.Regions)
        {
            if (match.Regions.ContainsKey(regionDef.Id))
            {
                continue;
            }

            match.Regions[regionDef.Id] = new Region
            {
                Id = regionDef.Id,
                OriginalOwnerId = null,
                OwnerId = null,
                SoldierCount = match.Room.GreyRegionDefenseCount
            };
        }

        match.Status = MatchStatus.Playing;
        match.StartedAtUtc = now;
        _logger.LogInformation("Maç başladı: {MatchId}, oyuncu sayısı {PlayerCount}", match.Id, confirmedPlayers.Count);
        _eventLogWriter.Log(match.Id, MatchEventType.MatchStarted, new
        {
            playerCount = confirmedPlayers.Count,
            playerIds = confirmedPlayers.Select(p => p.Id).ToList()
        });
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

    public void MarkDisconnected(string connectionId, DateTime now)
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
                    if (match.Status == MatchStatus.Playing)
                    {
                        player.DisconnectedAtUtc = now;
                    }
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
            player.DisconnectedAtUtc = null;
            _connections[connectionId] = (match.Id, playerId);
        }
    }
}
