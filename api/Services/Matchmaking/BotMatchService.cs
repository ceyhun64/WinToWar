using api.Models;
using api.Models.Rooms;
using api.Services.GameEngine;

namespace api.Services.Matchmaking;

/// <summary>
/// docs/03-game-rules.md Bölüm 7 (DÜZELTME — "bot yok" kararı geri alındı) ve
/// Bölüm 13 "Diğer Dosyalara Etki Özeti": bot eşleştirme (lobi doldurma) ve
/// zorluk seçimi burada toplanır. İki sorumluluk:
///
/// 1. <see cref="FillLobbyWithBots"/> — Standart/Practice lobisi, ilk gerçek
///    oyuncudan GameConfig.BotMatchWaitMinSeconds-MaxSeconds sonra hâlâ dolmadıysa
///    kalan koltukları bot ile doldurur (VIP'de asla — kurucu "Şimdi Başlat" ile
///    kendi kontrolündedir).
/// 2. <see cref="ProcessBotDecisions"/> — Playing durumundaki bir maçta her bot
///    için, zorluk profiline göre bir saldırı fırsatı değerlendirir. Sonuç
///    MovementService/CombatService'in AYNI gerçek motoruyla hesaplanır — sıraya,
///    kimliğe veya "kaçıncı maç" bilgisine göre önceden belirlenmez (Bölüm 7 🔒).
///
/// Bot, hiçbir zaman Wallet/PaymentInvoice edinmez (bkz. Player.IsBot dokümantasyonu)
/// — bu yüzden PayoutService'in havuz hesabına hiç girmez, gerçek para akışına
/// dokunmaz.
/// </summary>
public class BotMatchService
{
    private readonly MatchManager _matchManager;
    private readonly MapProvider _mapProvider;
    private readonly MovementService _movementService;
    private readonly ILogger<BotMatchService> _logger;

    public BotMatchService(MatchManager matchManager, MapProvider mapProvider, MovementService movementService, ILogger<BotMatchService> logger)
    {
        _matchManager = matchManager;
        _mapProvider = mapProvider;
        _movementService = movementService;
        _logger = logger;
    }

    /// <summary>
    /// Lobi henüz doluysa/VIP ise/kapandıysa hiçbir şey yapmaz. Aksi halde kalan
    /// tüm koltukları bot ile doldurup (her biri kendi zorluk profiliyle) lobiyi
    /// doldurur — mevcut ReservePlayer/ConfirmPlayerPayment akışı yeniden kullanılır,
    /// bu yüzden "lobi dolunca Countdown'a geç" mantığı burada tekrarlanmaz.
    /// </summary>
    public List<Player> FillLobbyWithBots(Match match, DateTime now)
    {
        var addedBots = new List<Player>();

        if (match.Room.Type == RoomType.Vip)
        {
            return addedBots;
        }

        int slotsToFill;
        lock (match.Lock)
        {
            if (match.Status != MatchStatus.Lobby)
            {
                return addedBots;
            }

            slotsToFill = match.Room.MaxPlayers - match.Players.Count;
        }

        for (var i = 0; i < slotsToFill; i++)
        {
            var difficulty = PickDifficulty();
            var botNumber = addedBots.Count + 1;
            Player bot;
            try
            {
                bot = _matchManager.ReservePlayer(match, $"Bot {botNumber}", now, isBot: true, botDifficulty: difficulty);
            }
            catch (InvalidOperationException)
            {
                // Lobi bu sırada başka bir yoldan (gerçek oyuncu/paralel çağrı) doldu/kapandı.
                break;
            }

            _matchManager.ConfirmPlayerPayment(match.Id, bot.Id, now);
            addedBots.Add(bot);
        }

        if (addedBots.Count > 0)
        {
            _logger.LogInformation("Lobi bot ile dolduruldu: {MatchId}, eklenen bot sayısı {BotCount}", match.Id, addedBots.Count);
        }

        return addedBots;
    }

    /// <summary>Match.Lock zaten tutuluyorken (EconomyTickService.Tick içinden) çağrılmalıdır.</summary>
    public void ProcessBotDecisions(Match match, DateTime now)
    {
        foreach (var bot in match.Players.Where(p => p.IsBot && !p.IsEliminated))
        {
            var interval = bot.BotDifficulty switch
            {
                BotDifficulty.Hard => GameConfig.BotDecisionIntervalSecondsHard,
                BotDifficulty.Easy => GameConfig.BotDecisionIntervalSecondsEasy,
                _ => GameConfig.BotDecisionIntervalSecondsNormal
            };

            if ((now - bot.LastActionAtUtc).TotalSeconds < interval)
            {
                continue;
            }

            bot.LastActionAtUtc = now;
            TryIssueAttack(match, bot, now);
        }
    }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 6: "tek bölgeden aynı anda birden fazla hedefe
    /// gönderim yok" — bot da bu tick'te en fazla bir saldırı emri verir. Hedef
    /// seçimi: fethedilebilir (A_i &gt; d_i, Bölüm 5 "+2 kuralı") komşulardan en
    /// zayıf olanı — bu, "sonuç önceden belirlenmez" kuralıyla tutarlı basit ama
    /// gerçek bir karar mekanizmasıdır (rastgele/sabit değil, oyun durumuna bakar).
    /// </summary>
    private void TryIssueAttack(Match match, Player bot, DateTime now)
    {
        var ownedRegions = match.Regions.Values.Where(r => r.OwnerId == bot.Id).ToList();

        foreach (var region in ownedRegions)
        {
            var sendable = region.SoldierCount - GameConfig.MinGarrisonPerSend;
            if (sendable <= 0)
            {
                continue;
            }

            // Kolay zorluk: yalnızca büyük bir fazlalık birikince saldırır (pasif profil).
            if (bot.BotDifficulty == BotDifficulty.Easy && sendable < GameConfig.BotEasyMinSurplusToAttack)
            {
                continue;
            }

            if (!_mapProvider.RegionsById.TryGetValue(region.Id, out var regionDef))
            {
                continue;
            }

            var target = regionDef.Neighbors
                .Select(id => match.Regions.TryGetValue(id, out var r) ? r : null)
                .Where(r => r is not null && r.OwnerId != bot.Id && sendable > r.SoldierCount)
                .OrderBy(r => r!.SoldierCount)
                .FirstOrDefault();

            if (target is null)
            {
                continue;
            }

            _movementService.DepartArmy(match, bot, region, target.Id, now);
            return;
        }
    }

    private static BotDifficulty PickDifficulty()
    {
        var roll = Random.Shared.Next(0, 100);
        if (roll < GameConfig.BotDifficultyEasyWeight)
        {
            return BotDifficulty.Easy;
        }

        if (roll < GameConfig.BotDifficultyEasyWeight + GameConfig.BotDifficultyNormalWeight)
        {
            return BotDifficulty.Normal;
        }

        return BotDifficulty.Hard;
    }
}
