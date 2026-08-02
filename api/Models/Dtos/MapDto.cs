namespace api.Models.Dtos;

/// <summary>Statik harita verisi — client'a bir kez gönderilir (bkz. MatchesController.GetMap).</summary>
public class MapDto
{
    public required List<MapRegionDto> Regions { get; init; }
}

public class MapRegionDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public required List<string> NeighborIds { get; init; }
}

/// <summary>
/// GameConfig'in arayüzde (maliyet gösterimi, buton disabled durumu vb.) ihtiyaç
/// duyulan alt kümesi. Frontend'de sayısal değerleri tekrar hardcode etmemek için
/// tek doğruluk kaynağı (GameConfig) buradan yansıtılır; gerçek doğrulama her zaman
/// sunucuda yapılır.
/// </summary>
public class GameConfigDto
{
    public required int SoldierCost { get; init; }
    public required int GeneralCost { get; init; }
    public required int NestUpgradeToLevel2Cost { get; init; }
    public required int NestUpgradeToLevel3Cost { get; init; }
    public required int MaxNestLevel { get; init; }
    public required int MaxGeneralsPerPlayer { get; init; }
    public required int MatchDurationSeconds { get; init; }
}
