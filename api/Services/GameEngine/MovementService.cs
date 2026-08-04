using api.Models;

namespace api.Services.GameEngine;

/// <summary>
/// Ordu hareketi. Bölgeler arası hareket anlık değildir; sabit bir süre kullanılır
/// (GameConfig.MovementDurationSeconds). Saldırı yalnızca doğrudan komşu bölgeye
/// yapılabilir; sürükle-bırak ile her gönderim tek hop'tur (docs/03-game-rules.md
/// Bölüm 6/15 — state.io incelemesi sonrası) — client asker sayısı belirtmez,
/// kaynak bölgenin mevcut askerinden GameConfig.MinGarrisonPerSend çıkarılarak
/// kalanının tamamı gönderilir.
///
/// Bu sınıftaki metotlar çağrılırken ilgili Match.Lock tutuluyor olmalıdır.
/// </summary>
public class MovementService
{
    private readonly MapProvider _mapProvider;
    private readonly CombatService _combatService;
    private readonly ILogger<MovementService> _logger;

    public MovementService(MapProvider mapProvider, CombatService combatService, ILogger<MovementService> logger)
    {
        _mapProvider = mapProvider;
        _combatService = combatService;
        _logger = logger;
    }

    public Army DepartArmy(Match match, Player player, Region fromRegion, string toRegionId, DateTime now)
    {
        if (fromRegion.OwnerId != player.Id)
        {
            throw new InvalidOperationException("Bu bölge size ait değil.");
        }

        if (GameConfig.AttackAdjacencyOnly && !_mapProvider.AreNeighbors(fromRegion.Id, toRegionId))
        {
            throw new InvalidOperationException("Saldırı yalnızca doğrudan komşu bölgeye yapılabilir.");
        }

        var soldierCount = fromRegion.SoldierCount - GameConfig.MinGarrisonPerSend;
        if (soldierCount <= 0)
        {
            throw new InvalidOperationException("Bu bölgede gönderilecek yeterli asker yok.");
        }

        fromRegion.SoldierCount -= soldierCount;

        var army = new Army
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = player.Id,
            SoldierCount = soldierCount,
            FromRegionId = fromRegion.Id,
            ToRegionId = toRegionId,
            DepartedAtUtc = now,
            ArrivesAtUtc = now.AddSeconds(GameConfig.MovementDurationSeconds)
        };
        match.Armies.Add(army);

        _logger.LogInformation(
            "Ordu yola çıktı: {From} -> {To}, asker {Soldiers}, varış {Arrival}",
            fromRegion.Id, army.ToRegionId, soldierCount, army.ArrivesAtUtc);

        return army;
    }

    /// <summary>
    /// Varan orduları işler: kendi bölgesine varan takviye garnizona katılır, aksi
    /// halde çatışma çözülür ve hayatta kalan tüm saldıranlar (varsa) yeni garrison
    /// olur — otomatik zincirleme bir sonraki bölgeye devam etmez (tek hop). Aynı
    /// bölgeye varan birden fazla ordu, varış sırasına göre (eşit zamanda Army.Id
    /// sırası) tek tek işlenir — hiçbir zaman birleşik güç olarak toplanmaz.
    /// </summary>
    public List<Army> ProcessArrivals(Match match, DateTime now)
    {
        var arrived = match.Armies
            .Where(a => a.ArrivesAtUtc <= now)
            .OrderBy(a => a.ArrivesAtUtc)
            .ThenBy(a => a.Id, StringComparer.Ordinal)
            .ToList();

        foreach (var army in arrived)
        {
            match.Armies.Remove(army);
            if (!match.Regions.TryGetValue(army.ToRegionId, out var region))
            {
                continue;
            }

            if (region.OwnerId == army.OwnerId)
            {
                region.SoldierCount += army.SoldierCount;
                _logger.LogInformation("Takviye ulaştı: {RegionId}, asker {Soldiers}", region.Id, army.SoldierCount);
                continue;
            }

            var owner = match.Players.FirstOrDefault(p => p.Id == army.OwnerId);
            if (owner is null || owner.IsEliminated)
            {
                continue;
            }

            _combatService.ResolveAttack(match, army, region, now);
        }

        return arrived;
    }
}
