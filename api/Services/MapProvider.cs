using System.Text.Json;
using api.Models;

namespace api.Services;

/// <summary>
/// Data/map.json içindeki statik harita verisini uygulama başlangıcında bir kez
/// okuyup bellekte tutar. Yeni harita eklemek için sadece yeni bir JSON dosyası
/// yazmak yeterlidir; bu sınıf değişmez. Başlangıçta komşuluk simetrisini
/// (A'nın komşusu B ise B'nin komşusu da A olmalı) doğrular.
/// </summary>
public class MapProvider
{
    public MapDefinition Map { get; }
    public IReadOnlyDictionary<string, MapRegionDefinition> RegionsById { get; }
    public int RegionCount => Map.Regions.Count;

    public MapProvider(IHostEnvironment env, ILogger<MapProvider> logger)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "map.json");
        var json = File.ReadAllText(path);
        var map = JsonSerializer.Deserialize<MapDefinition>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Harita verisi (map.json) okunamadı.");

        if (map.Regions.Count != GameConfig.RegionCount)
        {
            throw new InvalidOperationException(
                $"Harita {GameConfig.RegionCount} bölge içermeli, ancak {map.Regions.Count} bulundu.");
        }

        RegionsById = map.Regions.ToDictionary(r => r.Id);

        foreach (var region in map.Regions)
        {
            if (region.Neighbors.Count != GameConfig.NeighborsPerRegion)
            {
                throw new InvalidOperationException(
                    $"Bölge '{region.Id}' {GameConfig.NeighborsPerRegion} komşuya sahip olmalı, ancak {region.Neighbors.Count} bulundu.");
            }

            // "Geometri bütünlüğü": bir bölge çizilebilir bir kapalı şekil olmalı.
            //
            // Eski sınır 4-8 köşeydi (docs/14-game-map-redesign.md Bölüm 3) — o zamanki
            // harita elle çizilmiş basit çokgenlerden oluşuyordu. docs/23-game-visual-refresh.md
            // §6.4 🔒 ile bölge siluetleri Lüksemburg'un 12 gerçek kantonunun sınırlarına
            // geçti; bu sınırlar nehirleri/vadileri izlediği için bölge başına 69-170 köşe
            // taşır ve eski üst sınır uygulamayı açılışta durduruyordu. Alt sınır (kapalı
            // bir yüzey için en az 3 köşe gerekir, güvenli pay ile 4) korunur, üst sınır
            // yalnızca bozuk/şişmiş veriye karşı bir emniyet freni olarak 500'e alınır.
            if (region.Geometry.Points.Count is < 4 or > 500)
            {
                throw new InvalidOperationException(
                    $"Bölge '{region.Id}' geometrisi 4-500 köşe içermeli, ancak {region.Geometry.Points.Count} köşe bulundu.");
            }

            foreach (var neighborId in region.Neighbors)
            {
                if (!RegionsById.TryGetValue(neighborId, out var neighbor))
                {
                    throw new InvalidOperationException($"Bölge '{region.Id}' bilinmeyen bir komşuya sahip: '{neighborId}'.");
                }

                if (!neighbor.Neighbors.Contains(region.Id))
                {
                    throw new InvalidOperationException(
                        $"Komşuluk simetrik değil: '{region.Id}' -> '{neighborId}' var ama tersi yok.");
                }
            }
        }

        Map = map;
        logger.LogInformation("Harita yüklendi: {RegionCount} bölge", map.Regions.Count);
    }

    public bool AreNeighbors(string regionAId, string regionBId)
    {
        return RegionsById.TryGetValue(regionAId, out var region) && region.Neighbors.Contains(regionBId);
    }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 3: her maç başında haritadaki bölgelerden rastgele
    /// N tanesi başlangıç kalesi olarak seçilir (N &lt;= RegionCount, bkz. Program.cs
    /// startup guard'ı). Kalan bölgeler nötr/gri başlar.
    /// </summary>
    public List<string> PickRandomStartingRegionIds(int playerCount)
    {
        if (playerCount > RegionCount)
        {
            throw new InvalidOperationException(
                $"Oyuncu sayısı ({playerCount}) haritadaki bölge sayısını ({RegionCount}) aşamaz.");
        }

        return Map.Regions
            .Select(r => r.Id)
            .OrderBy(_ => Random.Shared.Next())
            .Take(playerCount)
            .ToList();
    }
}
