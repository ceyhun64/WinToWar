using api.Models;
using api.Services;

namespace api.Services.GameEngine;

/// <summary>Bir saldırının sonucu.</summary>
public record AttackResult(bool Captured);

/// <summary>
/// Saldırı/savunma çözümleme mantığı (docs/03-game-rules.md Bölüm 5 "+2 Asker Kuralı").
/// Bölge ele geçirme şartı: A (saldıran asker) &gt;= d (bölgenin savunması) + 1.
/// Ele geçirilince hayatta kalan TÜM saldıran askerler garrison olarak kalır (Bölüm 6/15
/// — sürükle-bırak her zaman kaynak bölgenin tüm askerini tek bir komşuya gönderir, otomatik
/// zincirleme bir sonraki bölgeye devam etmez). Saldırı püskürtülürse saldıran TÜM
/// gönderdiği askerleri kaybeder, savunan taraf saldıran kadar kayıp verir (sıfırın altına
/// inmez).
///
/// Bu sınıftaki metotlar çağrılırken ilgili Match.Lock tutuluyor olmalıdır (bkz.
/// MovementService). Aynı bölgeye varan birden fazla ordu, çağıran taraf tarafından
/// ArrivesAtUtc sırasına göre tek tek bu metoda verilir.
/// </summary>
public class CombatService
{
    private readonly MatchEventLogWriter _eventLogWriter;
    private readonly ILogger<CombatService> _logger;

    public CombatService(MatchEventLogWriter eventLogWriter, ILogger<CombatService> logger)
    {
        _eventLogWriter = eventLogWriter;
        _logger = logger;
    }

    public AttackResult ResolveAttack(Match match, Army army, Region targetRegion, DateTime now)
    {
        var defense = targetRegion.SoldierCount;

        _eventLogWriter.Log(match.Id, MatchEventType.RegionAttacked, new
        {
            attackerId = army.OwnerId,
            regionId = targetRegion.Id,
            attackerSoldiers = army.SoldierCount,
            defense
        });

        if (army.SoldierCount < defense + 1)
        {
            return RepelAttack(targetRegion, army.SoldierCount, defense);
        }

        return CaptureRegion(match, army, targetRegion, defense);
    }

    private AttackResult RepelAttack(Region targetRegion, int attackerSoldiers, int defense)
    {
        targetRegion.SoldierCount = Math.Max(0, defense - attackerSoldiers);
        _logger.LogInformation(
            "Saldırı püskürtüldü: {RegionId}, saldıran {Attacker} asker kaybetti, savunma {Defense}'e düştü",
            targetRegion.Id, attackerSoldiers, targetRegion.SoldierCount);
        return new AttackResult(Captured: false);
    }

    private AttackResult CaptureRegion(Match match, Army army, Region targetRegion, int defense)
    {
        var previousOwnerId = targetRegion.OwnerId;
        var garrison = army.SoldierCount - defense;

        targetRegion.OwnerId = army.OwnerId;
        targetRegion.SoldierCount = garrison;

        _logger.LogInformation(
            "Bölge ele geçirildi: {RegionId}, yeni sahip {OwnerId}, garrison {Garrison}",
            targetRegion.Id, army.OwnerId, garrison);
        _eventLogWriter.Log(match.Id, MatchEventType.RegionCaptured, new
        {
            regionId = targetRegion.Id,
            newOwnerId = army.OwnerId,
            previousOwnerId,
            garrison
        });

        if (previousOwnerId is not null)
        {
            HandlePreviousOwnerLoss(match, targetRegion, previousOwnerId, _eventLogWriter);
        }

        return new AttackResult(Captured: true);
    }

    /// <summary>Bir oyuncu, yalnızca KENDİ orijinal başlangıç kalesi ele geçirildiğinde elenir.</summary>
    private static void HandlePreviousOwnerLoss(Match match, Region targetRegion, string previousOwnerId, MatchEventLogWriter eventLogWriter)
    {
        if (targetRegion.OriginalOwnerId != previousOwnerId)
        {
            return;
        }

        var previousOwner = match.Players.FirstOrDefault(p => p.Id == previousOwnerId);
        if (previousOwner is not null && !previousOwner.IsEliminated)
        {
            EliminatePlayer(match, previousOwner, eventLogWriter);
        }
    }

    /// <summary>
    /// Elenen oyuncunun tuttuğu diğer (ele geçirdiği) bölgeler sahipsiz/nötr kalır ve
    /// odanın gri bölge savunma sayısına sıfırlanır (docs/03-game-rules.md Bölüm 8) —
    /// saldırganın rengine otomatik geçmez, orantısız güç sıçraması önlenir.
    /// Hareket halindeki tüm orduları da haritadan kalkar.
    /// </summary>
    internal static void EliminatePlayer(Match match, Player player, MatchEventLogWriter eventLogWriter)
    {
        player.IsEliminated = true;

        foreach (var region in match.Regions.Values.Where(r => r.OwnerId == player.Id))
        {
            region.OwnerId = null;
            region.SoldierCount = match.Room.GreyRegionDefenseCount;
        }

        match.Armies.RemoveAll(a => a.OwnerId == player.Id);
        eventLogWriter.Log(match.Id, MatchEventType.PlayerEliminated, new { playerId = player.Id });
    }
}
