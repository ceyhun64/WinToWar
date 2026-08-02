namespace api.Models;

/// <summary>
/// Data/map.json içinden okunan statik harita verisi. Yeni bir harita eklemek
/// için sadece yeni bir JSON dosyası yazmak yeterli olsun diye kod içine
/// hardcode edilmez (bkz. Bölüm 6.1).
/// </summary>
public class MapNeighbor
{
    public required string RegionId { get; init; }
    public double DistanceUnits { get; init; }
}

public class MapRegionDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public bool IsStartingRegion { get; init; }
    public int? StartingPlayerSlot { get; init; }
    public List<MapNeighbor> Neighbors { get; init; } = new();
}

public class MapDefinition
{
    public List<MapRegionDefinition> Regions { get; init; } = new();
}
