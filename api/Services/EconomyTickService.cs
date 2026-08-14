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
                TickResult tickResult;
                lock (match.Lock)
                {
                    tickResult = Tick(match, now);
                    winners = match.Status == MatchStatus.Completed ? match.Winners.ToList() : null;
                }

                await BroadcastState(match, cancellationToken);
                await BroadcastArmyDepartures(match, tickResult.Departures, cancellationToken);
                await BroadcastArmyArrivals(match, tickResult.ArrivedArmies, cancellationToken);

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

    /// <summary>docs/19-army.md: bir tick içinde yola çıkan (Departures) ve hedefe ulaşan (ArrivedArmies) ordu grupları — çağıran taraf (TickMatchAsync) bunları ayrı event'lerle yayınlar.</summary>
    internal record TickResult(List<ArmyDepartureResult> Departures, List<Army> ArrivedArmies);

    /// <summary>
    /// Match.Lock altında çağrılır. Unit testler için internal (bkz. InternalsVisibleTo).
    /// </summary>
    internal TickResult Tick(Match match, DateTime now)
    {
        ApplyProduction(match, now);
        ApplyNeutralRegionRegen(match, now);
        // docs/19-army.md: kaynak bölgelerden kademeli olarak vadesi gelen sevkiyat
        // gruplarını gerçek Army kayıtlarına dönüştürür — üretimden SONRA, varıştan
        // ÖNCE çalışır ki aynı tick içinde hem üretilen hem gönderilen asker doğru
        // sırayla region.SoldierCount'a yansısın (§20).
        var departures = _movementService.ProcessDispatches(match, now);
        var arrivedArmies = _movementService.ProcessArrivals(match, now);
        // docs/03-game-rules.md Bölüm 7 (DÜZELTME): bot kararları, güncel üretim/
        // varış sonrası tahtaya göre değerlendirilir — en son gerçek durumu kullanır.
        // docs/20-state-io-army-gorsel-fark-giderme.md §2.A.8: bot'un bu turda başlattığı
        // yeni bir dispatch'in ilk grubu da (yukarıdaki ProcessDispatches'ten SONRA,
        // henüz o listeye giremeden oluştuğu için) aynı departures listesine eklenir —
        // aksi halde bot saldırısı player'dan farklı, gecikmeli bir yoldan görünür olur.
        departures.AddRange(_botMatchService.ProcessBotDecisions(match, now));
        var newlyEliminated = ProcessAbandonment(match, now);
        EvaluateMatchEnd(match, now, newlyEliminated);
        return new TickResult(departures, arrivedArmies);
    }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 4 (müşteri kararıyla güncellendi — bkz. Bölüm 15-B.1):
    /// artık tek kaynaklı DEĞİL — sahip olunan HER bölge (Ana Kale dahil), Ana Kale ile
    /// birebir aynı oranda (`BaseProductionPerInterval`, her `ProductionIntervalSeconds`de
    /// bir) kendi askerini kendi üretir. Fethedilen bir bölge artık "1 asker"de takılı
    /// kalmaz, Ana Kale gibi büyümeye başlar. Eski "ConqueredRegionCount × BonusPerRegion"
    /// merkezi bonus formülü bu değişiklikle gereksizleşti ve kaldırıldı — daha fazla
    /// bölge tutmanın karşılığı artık dolaylı bir bonus değil, doğrudan o bölgelerin
    /// kendi üretimidir. Tick saniyelik olduğundan üretim GameConfig.ProductionIntervalSeconds'a
    /// göre kesirli birikimle işlenir — bunun için basit ve deterministik bir yaklaşım
    /// kullanılır: maç başlangıcından bu yana geçen tam interval sayısı hesaplanıp bir
    /// önceki tick'teki tam interval sayısıyla karşılaştırılır.
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

        foreach (var region in match.Regions.Values)
        {
            if (region.OwnerId is not { } ownerId)
            {
                continue;
            }

            var owner = match.Players.FirstOrDefault(p => p.Id == ownerId);
            if (owner is null || owner.IsEliminated)
            {
                continue;
            }

            // Bölüm 4/12: turtling'i anlamsız kılan tavan — artık bölge-bazlı uygulanır
            // (önceden tek bir Ana Kale toplamına uygulanıyordu), her bölge kendi
            // sınırına ulaşınca durur, sınırın altına inince (asker gönderilince)
            // otomatik devam eder.
            region.SoldierCount = Math.Min(GameConfig.MaxAccumulatedTroops, region.SoldierCount + GameConfig.BaseProductionPerInterval);
        }
    }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 4 (yeni müşteri talimatı): fethedilmeyen (sahipsiz/nötr)
    /// bir bölge saldırıyla zayıflatılıp ele geçirilemezse (ör. 10 savunmaya 6 asker
    /// gönderilip püskürtülürse savunma 4'e düşer), o andan itibaren HER SANİYE +1
    /// kendiliğinden iyileşir — odanın savunma tavanına (`Room.GreyRegionDefenseCount`)
    /// ulaşınca durur. `GameConfig.GameTickMs` artık 1 saniyeden kısa olduğundan (bkz.
    /// GameConfig notu) `ApplyProduction` ile AYNI elapsed-time interval sayma deseni
    /// kullanılır — tick'in kendisi saniyede bir kereden fazla çalışsa bile bölge en
    /// fazla saniyede 1 kez artar. Yalnızca hâlâ sahipsiz bölgeleri etkiler — bir
    /// oyuncu tarafından ele geçirilmiş bölgeler bu mekanikten etkilenmez, onlar
    /// yukarıdaki bölge-bazlı üretim formülüyle büyür.
    /// </summary>
    private static void ApplyNeutralRegionRegen(Match match, DateTime now)
    {
        if (match.StartedAtUtc is not DateTime startedAt)
        {
            return;
        }

        var elapsedSeconds = (now - startedAt).TotalSeconds;
        var previousElapsedSeconds = elapsedSeconds - GameConfig.GameTickMs / 1000.0;
        var currentIntervals = (int)(elapsedSeconds / GameConfig.NeutralRegenIntervalSeconds);
        var previousIntervals = (int)(Math.Max(0, previousElapsedSeconds) / GameConfig.NeutralRegenIntervalSeconds);

        if (currentIntervals <= previousIntervals)
        {
            return;
        }

        foreach (var region in match.Regions.Values)
        {
            if (region.OwnerId is not null)
            {
                continue;
            }

            if (region.SoldierCount < match.Room.GreyRegionDefenseCount)
            {
                region.SoldierCount += 1;
            }
        }
    }

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

    /// <summary>
    /// docs/19-army.md: bu tick'te bir dispatch'ten kaynaktan gerçekten ayrılan her
    /// grup için ArmyDeparted (ve varsa karşı yönlü bir ordu ile aynı anda karşılaştıysa
    /// ArmyClashed) event'i yayınlar — GameHub.AttackRegion'daki ilk-grup broadcast'iyle
    /// aynı DTO mapping'i kullanır (MatchStateMapper.ToArmyDto).
    /// </summary>
    private async Task BroadcastArmyDepartures(Match match, List<ArmyDepartureResult> departures, CancellationToken cancellationToken)
    {
        foreach (var departure in departures)
        {
            await _hubContext.Clients.Group(match.Id).SendAsync("ArmyDeparted", new ArmyDepartedDto
            {
                Army = MatchStateMapper.ToArmyDto(departure.DepartedArmy)
            }, cancellationToken);

            if (departure.Clash is not null)
            {
                var clash = departure.Clash;
                await _hubContext.Clients.Group(match.Id).SendAsync("ArmyClashed", new ArmyClashedDto
                {
                    FirstArmyId = clash.FirstArmyId,
                    SecondArmyId = clash.SecondArmyId,
                    WinningArmyId = clash.WinningArmyId,
                    SurvivorCount = clash.SurvivorCount,
                    ClashAtUtc = clash.ClashAtUtc
                }, cancellationToken);
            }
        }
    }

    /// <summary>docs/15-asker-hareketi-performans.md Bölüm 6.3: her varan ordu için ayrı ayrı yayınlanır ki animasyon katmanı MatchState'in bir sonraki tick'ini beklemeden varış/pop animasyonunu oynatabilsin.</summary>
    private async Task BroadcastArmyArrivals(Match match, List<Army> arrivedArmies, CancellationToken cancellationToken)
    {
        foreach (var army in arrivedArmies)
        {
            await _hubContext.Clients.Group(match.Id).SendAsync("ArmyArrived", new ArmyArrivedDto
            {
                ArmyId = army.Id,
                OwnerId = army.OwnerId,
                SoldierCount = army.SoldierCount,
                RegionId = army.ToRegionId
            }, cancellationToken);
        }
    }
}
