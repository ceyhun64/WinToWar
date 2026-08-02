using System.Text.Json;
using api.Models;

namespace api.Services;

/// <summary>
/// Data/map.json içindeki statik harita verisini uygulama başlangıcında bir kez
/// okuyup bellekte tutar. Yeni harita eklemek için sadece yeni bir JSON dosyası
/// yazmak yeterlidir; bu sınıf değişmez.
/// </summary>
public class MapProvider
{
    public MapDefinition Map { get; }
    public IReadOnlyDictionary<string, MapRegionDefinition> RegionsById { get; }

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

        foreach (var region in map.Regions)
        {
            if (region.Neighbors.Count != GameConfig.NeighborsPerRegion)
            {
                throw new InvalidOperationException(
                    $"Bölge '{region.Id}' {GameConfig.NeighborsPerRegion} komşuya sahip olmalı, ancak {region.Neighbors.Count} bulundu.");
            }
        }

        Map = map;
        RegionsById = map.Regions.ToDictionary(r => r.Id);
        logger.LogInformation("Harita yüklendi: {RegionCount} bölge", map.Regions.Count);
    }

    public bool AreNeighbors(string regionAId, string regionBId)
    {
        return RegionsById.TryGetValue(regionAId, out var region) &&
               region.Neighbors.Any(n => n.RegionId == regionBId);
    }

    public double GetDistance(string regionAId, string regionBId)
    {
        if (!RegionsById.TryGetValue(regionAId, out var region))
        {
            throw new InvalidOperationException($"Bilinmeyen bölge: {regionAId}");
        }

        var neighbor = region.Neighbors.FirstOrDefault(n => n.RegionId == regionBId);
        if (neighbor is null)
        {
            throw new InvalidOperationException($"'{regionAId}' ve '{regionBId}' komşu değil.");
        }

        return neighbor.DistanceUnits;
    }
}
