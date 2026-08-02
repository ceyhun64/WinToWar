using api.Models;
using api.Models.Dtos;
using api.Services;
using api.Services.GameEngine;
using Microsoft.AspNetCore.SignalR;

namespace api.Hubs;

/// <summary>
/// Oyuncu bağlantıları, oda/maç yönetimi ve gerçek zamanlı aksiyon mesajları.
/// Sunucu otoriterdir: her aksiyon burada doğrulanır, client'tan gelen veriye
/// güvenilmez. Her state mutasyonu ilgili Match.Lock altında yapılır, ardından
/// güncel durum DTO'ya map'lenip gruba yayınlanır.
/// </summary>
public class GameHub : Hub
{
    private readonly MatchManager _matchManager;
    private readonly MovementService _movementService;
    private readonly UpgradeService _upgradeService;
    private readonly ILogger<GameHub> _logger;

    public GameHub(
        MatchManager matchManager,
        MovementService movementService,
        UpgradeService upgradeService,
        ILogger<GameHub> logger)
    {
        _matchManager = matchManager;
        _movementService = movementService;
        _upgradeService = upgradeService;
        _logger = logger;
    }

    public async Task JoinMatch(string matchId, string playerId)
    {
        if (!_matchManager.TryGetMatch(matchId, out var match))
        {
            await Clients.Caller.SendAsync("ActionError", "Maç bulunamadı.");
            return;
        }

        try
        {
            _matchManager.ReconnectPlayer(match, playerId, Context.ConnectionId);
        }
        catch (InvalidOperationException ex)
        {
            await Clients.Caller.SendAsync("ActionError", ex.Message);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, matchId);
        await BroadcastState(match);
    }

    public async Task TrainSoldier(string regionId)
    {
        await HandleAction(regionId, (match, player, region) =>
        {
            if (region.OwnerId != player.Id || region.Nest is null)
            {
                throw new InvalidOperationException("Bu bölgede size ait bir yuva yok.");
            }

            if (player.Gold < GameConfig.SoldierCost)
            {
                throw new InvalidOperationException("Yeterli altın yok.");
            }

            player.Gold -= GameConfig.SoldierCost;
            region.Nest.GarrisonSoldiers += 1;
        });
    }

    public async Task TrainGeneral(string regionId)
    {
        await HandleAction(regionId, (match, player, region) =>
        {
            if (region.OwnerId != player.Id || region.Nest is null)
            {
                throw new InvalidOperationException("Bu bölgede size ait bir yuva yok.");
            }

            var aliveGenerals = match.Generals.Count(g => g.OwnerId == player.Id && g.Status != GeneralStatus.Dead);
            if (aliveGenerals >= GameConfig.MaxGeneralsPerPlayer)
            {
                throw new InvalidOperationException("Maksimum General sayısına ulaşıldı.");
            }

            if (player.Gold < GameConfig.GeneralCost)
            {
                throw new InvalidOperationException("Yeterli altın yok.");
            }

            player.Gold -= GameConfig.GeneralCost;
            match.Generals.Add(new General
            {
                Id = Guid.NewGuid().ToString("N"),
                OwnerId = player.Id,
                Status = GeneralStatus.Garrisoned,
                CurrentRegionId = region.Id
            });
        });
    }

    public async Task UpgradeNest(string regionId)
    {
        await HandleAction(regionId, (match, player, region) =>
        {
            _upgradeService.Upgrade(player, region);
        });
    }

    public async Task AttackRegion(string fromRegionId, string toRegionId, string generalId, int soldierCount)
    {
        await HandleAction(fromRegionId, (match, player, region) =>
        {
            var general = match.Generals.FirstOrDefault(g => g.Id == generalId)
                ?? throw new InvalidOperationException("General bulunamadı.");

            if (general.OwnerId != player.Id)
            {
                throw new InvalidOperationException("Bu General size ait değil.");
            }

            _movementService.DepartArmy(match, player, general, region, toRegionId, soldierCount);
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _matchManager.MarkDisconnected(Context.ConnectionId);
        if (_matchManager.TryGetByConnection(Context.ConnectionId, out var matchId, out _) &&
            _matchManager.TryGetMatch(matchId, out var match))
        {
            await BroadcastState(match);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Ortak aksiyon iskeleti: oyuncuyu ve (gerekliyse) kaynak bölgeyi doğrular,
    /// mutasyonu Match.Lock altında uygular, ardından güncel durumu yayınlar.
    /// Hata durumunda sadece isteği yapan client'a ActionError gönderilir.
    /// </summary>
    private async Task HandleAction(string regionId, Action<Match, Player, Region> action)
    {
        if (!_matchManager.TryGetByConnection(Context.ConnectionId, out var matchId, out var playerId) ||
            !_matchManager.TryGetMatch(matchId, out var match))
        {
            await Clients.Caller.SendAsync("ActionError", "Bir maça bağlı değilsiniz.");
            return;
        }

        try
        {
            lock (match.Lock)
            {
                var player = match.Players.FirstOrDefault(p => p.Id == playerId)
                    ?? throw new InvalidOperationException("Oyuncu bulunamadı.");

                if (match.Status != MatchStatus.InProgress)
                {
                    throw new InvalidOperationException("Maç şu anda devam etmiyor.");
                }

                if (player.IsEliminated)
                {
                    throw new InvalidOperationException("Elendiniz, aksiyon alamazsınız.");
                }

                if (!match.Regions.TryGetValue(regionId, out var region))
                {
                    throw new InvalidOperationException("Bölge bulunamadı.");
                }

                action(match, player, region);
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation("Aksiyon reddedildi: {Message}", ex.Message);
            await Clients.Caller.SendAsync("ActionError", ex.Message);
            return;
        }

        await BroadcastState(match);
    }

    private async Task BroadcastState(Match match)
    {
        MatchStateDto dto;
        lock (match.Lock)
        {
            dto = MatchStateMapper.ToDto(match);
        }

        await Clients.Group(match.Id).SendAsync("MatchState", dto);
    }
}
