namespace api.Models;

/// <summary>
/// Data/map.json içinden okunan statik harita verisi. WinToWar'da başlangıç kalesi
/// ataması sabit değil, her maçta rastgele yapılır (bkz. docs/03-game-rules.md
/// Bölüm 3 "Rastgele kale ataması") — bu yüzden isStartingRegion/startingPlayerSlot
/// alanları kullanılmaz, harita yalnızca kimlik/komşuluk/konum verisi taşır.
/// </summary>
public class MapRegionDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public List<string> Neighbors { get; init; } = new();
}

public class MapDefinition
{
    public List<MapRegionDefinition> Regions { get; init; } = new();
}
