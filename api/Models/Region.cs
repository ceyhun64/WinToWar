namespace api.Models;

/// <summary>
/// Bir maç içindeki bölgenin çalışma zamanı (runtime) durumu: sahiplik, yuva ve
/// nötr garnizon bilgisi. Statik harita verisi (komşuluklar, koordinatlar) için
/// bkz. MapRegionDefinition.
/// </summary>
public class Region
{
    public required string Id { get; init; }
    public string? OwnerId { get; set; }
    public Nest? Nest { get; set; }
    public int NeutralDefenseSoldiers { get; set; }
}
