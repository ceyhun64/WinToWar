using api.Hubs;
using api.Models;
using api.Models.Dtos;
using api.Models.Payments;
using api.Services.Payments;
using api.Services.GameEngine;
using api.Services.Matchmaking;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

/// <summary>
/// Aktif tüm maçlar için saniyelik oyun tick'ini yürüten arka plan servisi. Maçın
/// durumuna göre farklı iş yapar: Lobby (docs/03-game-rules.md Bölüm 7 — süre
/// dolunca otomatik iptal YOK, tek seferlik bir "seçim zamanı" bildirimi yayınlanır),
/// Countdown (geri sayım dolunca haritayı kurup Playing'e geçirir), Playing (ekonomi
/// üretimi, hareket/çatışma, terk etme, maç bitiş koşulu + payout tetikleme).
///
/// PeriodicTimer + CancellationToken kullanılır ki uygulama kapanırken/servis
/// durdurulurken asılı kalan task veya kaynak sızıntısı olmasın.
/// </summary>
public class EconomyTickService : BackgroundService
{
    private readonly MatchManager _matchManager;
    private readonly MovementService _movementService;
    private readonly BotMatchService _botMatchService;
    private readonly MatchEventLogWriter _eventLogWriter;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EconomyTickService> _logger;

    public EconomyTickService(
        MatchManager matchManager,
        MovementService movementService,
        BotMatchService botMatchService,
        MatchEventLogWriter eventLogWriter,
        IHubContext<GameHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<EconomyTickService> logger)
    {
        _matchManager = matchManager;
        _movementService = movementService;
        _botMatchService = botMatchService;
        _eventLogWriter = eventLogWriter;
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(GameConfig.GameTickMs));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = DateTime.UtcNow;

            foreach (var match in _matchManager.ActiveMatches)
            {
                await TickMatchAsync(match, now, stoppingToken);
            }
        }
    }

    private async Task TickMatchAsync(Match match, DateTime now, CancellationToken cancellationToken)
    {
        switch (match.Status)
        {
            case MatchStatus.Lobby:
                await TickLobbyAsync(match, now, cancellationToken);
                break;

            case MatchStatus.Countdown:
                bool started;
                lock (match.Lock)
                {
                    started = now >= match.CountdownEndsAtUtc;
                    if (started)
                    {
                        _matchManager.StartPlaying(match, now);
                    }
                }
                if (started)
                {
                    await BroadcastState(match, cancellationToken);
                }
                break;

            case MatchStatus.Playing:
                List<string>? winners;
                lock (match.Lock)
                {
                    Tick(match, now);
                    winners = match.Status == MatchStatus.Completed ? match.Winners.ToList() : null;
                }

                await BroadcastState(match, cancellationToken);

                if (winners is { Count: > 0 } && !match.Room.IsPractice)
                {
                    await TriggerPayoutAsync(match.Id, winners, cancellationToken);
                }
                break;
        }
    }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 7: zaman aşımında otomatik iade/iptal YOK. Tek
    /// seferlik bir "seçim zamanı geldi" bildirimi yayınlanır (LobbyTimeoutReached);
    /// oyuncu bundan sonra istediği an GameHub.LeaveLobby ile ayrılıp refund alabilir,
    /// ya da hiçbir şey yapmayıp beklemeye devam edebilir — sayaç sıfırlanmaz.
    /// </summary>
    private async Task TickLobbyAsync(Match match, DateTime now, CancellationToken cancellationToken)
    {
        bool shouldNotify;
        var timeoutSeconds = match.Room.IsPractice ? GameConfig.PracticeLobbyFillTimeoutSeconds : GameConfig.LobbyFillTimeoutSeconds;

        lock (match.Lock)
        {
            var timedOut = match.LobbyOpenedAtUtc is DateTime openedAt &&
                           (now - openedAt).TotalSeconds >= timeoutSeconds;

            shouldNotify = timedOut && !match.LobbyTimeoutNotified;
            if (shouldNotify)
            {
                match.LobbyTimeoutNotified = true;
            }
        }

        if (shouldNotify)
        {
            await _hubContext.Clients.Group(match.Id).SendAsync("LobbyTimeoutReached", cancellationToken: cancellationToken);
        }

        // docs/03-game-rules.md Bölüm 7 (DÜZELTME): 5 dakikalık (Practice 60 sn)
        // uzun zaman aşımından ayrı, çok daha kısa bir bot-doldurma penceresi —
        // FillLobbyWithBots kendi içinde VIP/lobi-durumu guard'larını uygular.
        var deadline = match.BotFillDeadlineUtc;
        if (deadline is DateTime botDeadline && now >= botDeadline)
        {
            var addedBots = _botMatchService.FillLobbyWithBots(match, now);
            if (addedBots.Count > 0)
            {
                await BroadcastState(match, cancellationToken);
            }
        }
    }

    /// <summary>
    /// docs/05-payment.md Bölüm 3.2: maç bir/birden fazla kazananla bittiğinde payout
    /// akışını tetikler. PayoutService.ProcessPayoutAsync kendi içinde idempotenttir
    /// (Payout.MatchId unique) — bu yüzden her tick'te güvenle tekrar çağrılabilir.
    /// </summary>
    private async Task TriggerPayoutAsync(string matchId, List<string> winnerPlayerIds, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var payoutService = scope.ServiceProvider.GetRequiredService<PayoutService>();
            await payoutService.ProcessPayoutAsync(matchId, winnerPlayerIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payout tetiklenirken hata: {MatchId}", matchId);
        }
    }

    /// <summary>Match.Lock altında çağrılır. Unit testler için internal (bkz. InternalsVisibleTo).</summary>
    internal void Tick(Match match, DateTime now)
    {
        ApplyProduction(match, now);
        _movementService.ProcessArrivals(match, now);
        // docs/03-game-rules.md Bölüm 7 (DÜZELTME): bot kararları, güncel üretim/
        // varış sonrası tahtaya göre değerlendirilir — en son gerçek durumu kullanır.
        _botMatchService.ProcessBotDecisions(match, now);
        var newlyEliminated = ProcessAbandonment(match, now);
        EvaluateMatchEnd(match, now, newlyEliminated);
    }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 4: ProductionPerInterval = BaseProduction(4) +
    /// ConqueredRegionCount × BonusPerRegion(1), her ProductionIntervalSeconds(10)
    /// saniyede bir, yalnızca hâlâ kendi ev kalesini elinde tutan oyuncular için.
    /// Tick saniyelik olduğundan üretim GameConfig.ProductionIntervalSeconds'a göre
    /// kesirli birikimle işlenir — bunun için her oyuncunun son üretim zamanı yerine,
    /// basit ve deterministik bir yaklaşım kullanılır: maç başlangıcından bu yana
    /// geçen tam interval sayısı hesaplanıp bir önceki tick'teki tam interval
    /// sayısıyla karşılaştırılır.
    /// </summary>
    private static void ApplyProduction(Match match, DateTime now)
    {
        if (match.StartedAtUtc is not DateTime startedAt)
        {
            return;
        }

        var elapsedSeconds = (now - startedAt).TotalSeconds;
        var previousElapsedSeconds = elapsedSeconds - GameConfig.GameTickMs / 1000.0;
        var currentIntervals = (int)(elapsedSeconds / GameConfig.ProductionIntervalSeconds);
        var previousIntervals = (int)(Math.Max(0, previousElapsedSeconds) / GameConfig.ProductionIntervalSeconds);

        if (currentIntervals <= previousIntervals)
        {
            return;
        }

        foreach (var player in match.Players.Where(p => !p.IsEliminated))
        {
            var homeRegion = match.Regions.Values.FirstOrDefault(r => IsProducingHomeRegionOf(r, player.Id));
            if (homeRegion is null)
            {
                continue;
            }

            var conqueredRegionCount = Math.Max(0, match.Regions.Values.Count(r => r.OwnerId == player.Id) - 1);
            var production = GameConfig.BaseProductionPerInterval + conqueredRegionCount * GameConfig.ProductionBonusPerRegion;
            // Bölüm 4/12: turtling'i anlamsız kılan tavan — sınıra ulaşınca üretim durur,
            // sınırın altına inince (asker gönderilince) otomatik devam eder.
            homeRegion.SoldierCount = Math.Min(GameConfig.MaxAccumulatedTroops, homeRegion.SoldierCount + production);
        }
    }

    /// <summary>
    /// docs/02-architecture.md "Models — entity'ler yalnızca veri ve durumu tutar":
    /// bu iş kuralı önceden Region.IsProducingHomeRegionOf olarak entity üzerindeydi,
    /// buraya (Service katmanına, tek çağrı noktası) taşındı (docs/09-eksik-tarama-promptu.md
    /// denetimi, Faz 8). Yalnızca hâlâ kendi orijinal sahibinin elindeyken üretim yapar.
    /// </summary>
    private static bool IsProducingHomeRegionOf(Region region, string playerId) =>
        region.OriginalOwnerId == playerId && region.OwnerId == playerId;

    /// <summary>Bağlantısı kopan oyuncu AbandonmentTimeoutSeconds içinde dönmezse otomatik elenir.</summary>
    private List<string> ProcessAbandonment(Match match, DateTime now)
    {
        var newlyEliminated = new List<string>();

        foreach (var player in match.Players.Where(p => !p.IsEliminated && p.DisconnectedAtUtc is not null))
        {
            var elapsed = (now - player.DisconnectedAtUtc!.Value).TotalSeconds;
            if (elapsed < GameConfig.AbandonmentTimeoutSeconds)
            {
                continue;
            }

            CombatService.EliminatePlayer(match, player, _eventLogWriter);
            newlyEliminated.Add(player.Id);
        }

        return newlyEliminated;
    }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 8: maç yalnızca tek oyuncu ayakta kalınca biter
    /// (süre sınırı yok). Son 2 oyuncu aynı tick'te elenirse (eşzamanlı eleme),
    /// ikisi de ortak kazanan sayılır — bu yüzden "remaining == 0" durumunda kazanan
    /// listesi bu tick'te elenenlerden (combat + abandonment) oluşturulur.
    /// </summary>
    private void EvaluateMatchEnd(Match match, DateTime now, List<string> newlyEliminatedByAbandonment)
    {
        var remaining = match.Players.Where(p => !p.IsEliminated).ToList();
        if (remaining.Count == 1)
        {
            CompleteMatch(match, now, [remaining[0].Id]);
            return;
        }

        if (remaining.Count == 0)
        {
            // Son 2 (veya daha fazla) oyuncu aynı tick içinde birlikte elendi.
            var lastEliminated = match.Players.Where(p => p.IsEliminated).Select(p => p.Id).ToList();
            CompleteMatch(match, now, lastEliminated);
        }
    }

    private void CompleteMatch(Match match, DateTime now, List<string> winners)
    {
        match.Status = MatchStatus.Completed;
        match.CompletedAtUtc = now;
        match.Winners.Clear();
        match.Winners.AddRange(winners);

        // docs/02-architecture.md "Maç Denetim Kaydı": yalnızca gerçek para akan
        // maçlar için gerekli (Practice hariç), ama kayıt burada koşulsuz düşülür —
        // TriggerPayoutAsync zaten IsPractice guard'ını ayrıca uygular; audit log
        // tarafında da aynı ayrımı tekrar etmek yerine tek yerde (burada) tutmak
        // yeterlidir, MatchEnded event'i Practice için de zararsızdır (yalnızca
        // /admin/maclar'da görünür, genel kullanıcıya hiç gösterilmez).
        _eventLogWriter.Log(match.Id, MatchEventType.MatchEnded, new { winners });
    }

    private async Task BroadcastState(Match match, CancellationToken cancellationToken)
    {
        MatchStateDto dto;
        lock (match.Lock)
        {
            dto = MatchStateMapper.ToDto(match, DateTime.UtcNow);
        }

        await _hubContext.Clients.Group(match.Id).SendAsync("MatchState", dto, cancellationToken);
    }
}
