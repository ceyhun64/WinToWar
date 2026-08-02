using api.Models;
using Microsoft.Extensions.Logging;

namespace api.Services.GameEngine;

/// <summary>
/// Ordu hareketi ve seyahat süresi hesaplama. Bölgeler arası hareket anlık değildir;
/// süre, harita JSON'undaki komşuluk mesafesine göre hesaplanıp 5-15 saniye aralığına
/// sıkıştırılır (bkz. GameConfig).
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

    public double GetTravelTimeSeconds(string fromRegionId, string toRegionId)
    {
        var distance = _mapProvider.GetDistance(fromRegionId, toRegionId);
        var seconds = distance * GameConfig.TravelSecondsPerDistanceUnit;
        return Math.Clamp(seconds, GameConfig.MinTravelTimeSeconds, GameConfig.MaxTravelTimeSeconds);
    }

    public Army DepartArmy(Match match, Player player, General general, Region fromRegion, string toRegionId, int soldierCount)
    {
        if (fromRegion.OwnerId != player.Id || fromRegion.Nest is null)
        {
            throw new InvalidOperationException("Bu bölge size ait değil.");
        }

        if (general.OwnerId != player.Id || general.Status != GeneralStatus.Garrisoned || general.CurrentRegionId != fromRegion.Id)
        {
            throw new InvalidOperationException("General bu bölgede saldırıya hazır değil.");
        }

        if (soldierCount <= 0)
        {
            throw new InvalidOperationException("Asker sayısı pozitif olmalıdır.");
        }

        if (fromRegion.Nest.GarrisonSoldiers < soldierCount)
        {
            throw new InvalidOperationException("Bu bölgede yeterli asker yok.");
        }

        if (!_mapProvider.AreNeighbors(fromRegion.Id, toRegionId))
        {
            throw new InvalidOperationException("Hedef bölge komşu değil, saldırı yalnızca komşu bölgelere yapılabilir.");
        }

        fromRegion.Nest.GarrisonSoldiers -= soldierCount;
        general.Status = GeneralStatus.Moving;
        general.CurrentRegionId = null;

        var travelSeconds = GetTravelTimeSeconds(fromRegion.Id, toRegionId);
        var now = DateTime.UtcNow;
        var army = new Army
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = player.Id,
            GeneralId = general.Id,
            SoldierCount = soldierCount,
            FromRegionId = fromRegion.Id,
            ToRegionId = toRegionId,
            DepartedAtUtc = now,
            ArrivesAtUtc = now.AddSeconds(travelSeconds)
        };
        match.Armies.Add(army);

        _logger.LogInformation(
            "Ordu yola çıktı: {From} -> {To}, asker {Soldiers}, varış {Arrival}",
            fromRegion.Id, toRegionId, soldierCount, army.ArrivesAtUtc);

        return army;
    }

    /// <summary>Varan orduları işler: kendi bölgesine varan takviye garnizona katılır, aksi halde çatışma çözülür.</summary>
    public List<Army> ProcessArrivals(Match match)
    {
        var now = DateTime.UtcNow;
        var arrived = match.Armies.Where(a => a.ArrivesAtUtc <= now).ToList();

        foreach (var army in arrived)
        {
            match.Armies.Remove(army);
            var region = match.Regions[army.ToRegionId];
            var general = match.Generals.FirstOrDefault(g => g.Id == army.GeneralId);
            if (general is null)
            {
                continue;
            }

            if (region.OwnerId == army.OwnerId && region.Nest is not null)
            {
                region.Nest.GarrisonSoldiers += army.SoldierCount;
                general.Status = GeneralStatus.Garrisoned;
                general.CurrentRegionId = region.Id;
                _logger.LogInformation("Takviye ulaştı: {RegionId}, asker {Soldiers}", region.Id, army.SoldierCount);
            }
            else
            {
                _combatService.ResolveAttack(match, army, region, general);
            }
        }

        return arrived;
    }
}
