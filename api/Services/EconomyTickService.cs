using api.Hubs;
using api.Models;
using api.Models.Dtos;
using api.Services.Payments;
using api.Services.GameEngine;
using Microsoft.AspNetCore.SignalR;

namespace api.Services;

/// <summary>
/// Aktif tüm maçlar için saniyelik oyun tick'ini yürüten arka plan servisi:
/// ekonomi üretimi (altın + asker, kesirli birikimle), ordu varışlarının
/// işlenmesi (MovementService'e delege — o da gerekirse CombatService'i çağırır),
/// General yeniden basımı ve maç bitiş koşullarının kontrolü.
///
/// PeriodicTimer + CancellationToken kullanılır ki uygulama kapanırken/servis
/// durdurulurken asılı kalan task veya kaynak sızıntısı olmasın.
/// </summary>
public class EconomyTickService : BackgroundService
{
    private readonly MatchManager _matchManager;
    private readonly MovementService _movementService;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EconomyTickService> _logger;

    public EconomyTickService(
        MatchManager matchManager,
        MovementService movementService,
        IHubContext<GameHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<EconomyTickService> logger)
    {
        _matchManager = matchManager;
        _movementService = movementService;
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(GameConfig.EconomyTickIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var match in _matchManager.ActiveMatches)
            {
                if (match.Status != MatchStatus.InProgress)
                {
                    continue;
                }

                lock (match.Lock)
                {
                    Tick(match);
                }

                await BroadcastState(match, stoppingToken);

                if (match.Status == MatchStatus.Finished && match.WinnerId is not null)
                {
                    await TriggerPayoutAsync(match.Id, match.WinnerId, stoppingToken);
                }
            }
        }
    }

    /// <summary>
    /// Bölüm 3.2 (docs/05-payment.md): maç bir kazananla bittiğinde payout akışını
    /// tetikler. PayoutService.ProcessPayoutAsync kendi içinde idempotenttir
    /// (Payout.MatchId unique) — bu yüzden her tick'te güvenle tekrar çağrılabilir.
    /// Ödeme modülü ayrı bir DI scope'unda (PaymentDbContext scoped) çalıştığından
    /// burada yeni bir scope açılır.
    /// </summary>
    private async Task TriggerPayoutAsync(string matchId, string winnerPlayerId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var payoutService = scope.ServiceProvider.GetRequiredService<PayoutService>();
            await payoutService.ProcessPayoutAsync(matchId, winnerPlayerId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payout tetiklenirken hata: {MatchId}", matchId);
        }
    }

    /// <summary>Match.Lock altında çağrılır. Unit testler için internal (bkz. InternalsVisibleTo).</summary>
    internal void Tick(Match match)
    {
        ApplyProduction(match);
        _movementService.ProcessArrivals(match);
        ProcessGeneralRespawns(match);
        EvaluateMatchEnd(match);
    }

    private static void ApplyProduction(Match match)
    {
        foreach (var region in match.Regions.Values)
        {
            var nest = region.Nest;
            if (nest is null)
            {
                continue;
            }

            var owner = match.Players.FirstOrDefault(p => p.Id == nest.OwnerId);
            if (owner is null)
            {
                continue;
            }

            var (goldPerMinute, soldiersPerMinute) = nest.Level switch
            {
                1 => (GameConfig.NestLevel1GoldPerMinute, GameConfig.NestLevel1SoldiersPerMinute),
                2 => (GameConfig.NestLevel2GoldPerMinute, GameConfig.NestLevel2SoldiersPerMinute),
                _ => (GameConfig.NestLevel3GoldPerMinute, GameConfig.NestLevel3SoldiersPerMinute)
            };

            owner.Gold += goldPerMinute / 60.0 * GameConfig.EconomyTickIntervalSeconds;

            nest.SoldierAccumulator += soldiersPerMinute / 60.0 * GameConfig.EconomyTickIntervalSeconds;
            var wholeSoldiers = (int)Math.Floor(nest.SoldierAccumulator);
            if (wholeSoldiers > 0)
            {
                nest.GarrisonSoldiers += wholeSoldiers;
                nest.SoldierAccumulator -= wholeSoldiers;
            }
        }
    }

    private void ProcessGeneralRespawns(Match match)
    {
        var now = DateTime.UtcNow;
        foreach (var general in match.Generals.Where(g =>
                     g.Status == GeneralStatus.Dead && g.RespawnAtUtc is not null && g.RespawnAtUtc <= now))
        {
            var owner = match.Players.FirstOrDefault(p => p.Id == general.OwnerId);
            if (owner is null || owner.Gold < GameConfig.GeneralRespawnCost)
            {
                continue; // Yeterli altın birikene kadar her tick'te tekrar denenir.
            }

            var homeRegion = match.Regions.Values
                .Where(r => r.OwnerId == general.OwnerId && r.Nest is not null)
                .OrderByDescending(r => r.Nest!.Level)
                .FirstOrDefault();

            if (homeRegion is null)
            {
                continue; // Oyuncunun artık hiç yuvası yoksa (elenmek üzere) yeniden basım yapılmaz.
            }

            owner.Gold -= GameConfig.GeneralRespawnCost;
            general.Status = GeneralStatus.Garrisoned;
            general.CurrentRegionId = homeRegion.Id;
            general.RespawnAtUtc = null;
            _logger.LogInformation("General yeniden doğdu: {GeneralId} -> {RegionId}", general.Id, homeRegion.Id);
        }
    }

    private void EvaluateMatchEnd(Match match)
    {
        var remaining = match.Players.Where(p => !p.IsEliminated).ToList();
        if (remaining.Count <= 1)
        {
            match.Status = MatchStatus.Finished;
            match.WinnerId = remaining.Count == 1 ? remaining[0].Id : null;
            _logger.LogInformation("Maç bitti (eleme): {MatchId}, kazanan {WinnerId}", match.Id, match.WinnerId);
            return;
        }

        if (match.StartedAtUtc is not DateTime startedAt)
        {
            return;
        }

        var elapsed = (DateTime.UtcNow - startedAt).TotalSeconds;
        if (elapsed < GameConfig.MatchDurationSeconds)
        {
            return;
        }

        var ranking = match.Players
            .Select(p => new { Player = p, RegionCount = match.Regions.Values.Count(r => r.OwnerId == p.Id) })
            .OrderByDescending(x => x.RegionCount)
            .ToList();

        var topCount = ranking[0].RegionCount;
        var topPlayers = ranking.Where(x => x.RegionCount == topCount).ToList();

        match.Status = MatchStatus.Finished;
        match.WinnerId = topPlayers.Count == 1 ? topPlayers[0].Player.Id : null;
        _logger.LogInformation("Maç bitti (süre doldu): {MatchId}, kazanan {WinnerId}", match.Id, match.WinnerId);
    }

    private async Task BroadcastState(Match match, CancellationToken cancellationToken)
    {
        MatchStateDto dto;
        lock (match.Lock)
        {
            dto = MatchStateMapper.ToDto(match);
        }

        await _hubContext.Clients.Group(match.Id).SendAsync("MatchState", dto, cancellationToken);
    }
}
