using System.Globalization;

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
    public required MapRegionGeometryDto Geometry { get; init; }
}

/// <summary>docs/14-game-map-redesign.md Bölüm 2: bölge şeklinin SVG polygon karşılığı.</summary>
public class MapRegionGeometryDto
{
    public required string Type { get; init; }
    public required List<double[]> Points { get; init; }
}

/// <summary>
/// GameConfig'in arayüzde (geri sayım, oda kurma formu limitleri vb.) ihtiyaç
/// duyulan alt kümesi. Frontend'de sayısal değerleri tekrar hardcode etmemek için
/// tek doğruluk kaynağı (GameConfig) buradan yansıtılır; gerçek doğrulama her zaman
/// sunucuda yapılır.
/// </summary>
public class GameConfigDto
{
    public required int StandardRoomPlayerCount { get; init; }
    public required int VipRoomMinPlayers { get; init; }
    public required int VipRoomMaxPlayers { get; init; }
    public required int GreyRegionDefenseMin { get; init; }
    public required int GreyRegionDefenseMax { get; init; }
    public required int PracticeRoomDefaultPlayerCount { get; init; }
    public required int BaseProductionPerInterval { get; init; }
    public required int ProductionIntervalSeconds { get; init; }
    public required int MovementDurationSeconds { get; init; }
    public required int LobbyFillTimeoutSeconds { get; init; }
    public required int PracticeLobbyFillTimeoutSeconds { get; init; }
    public required int LobbyCountdownSeconds { get; init; }
    public required int AbandonmentTimeoutSeconds { get; init; }
    public required int ResultScreenDurationSeconds { get; init; }
    public required string CommissionRate { get; init; }

    /// <summary>
    /// <paramref name="commissionRate"/> her zaman PaymentConfig.CommissionRate'ten
    /// gelir — GameConfig'te ayrı bir kopya tutulmaz (tek doğruluk kaynağı, bkz.
    /// docs/05-payment.md Bölüm 1.1).
    /// </summary>
    public static GameConfigDto FromGameConfig(decimal commissionRate) => new()
    {
        StandardRoomPlayerCount = GameConfig.StandardRoomPlayerCount,
        VipRoomMinPlayers = GameConfig.VipRoomMinPlayers,
        VipRoomMaxPlayers = GameConfig.VipRoomMaxPlayers,
        GreyRegionDefenseMin = GameConfig.GreyRegionDefenseMin,
        GreyRegionDefenseMax = GameConfig.GreyRegionDefenseMax,
        PracticeRoomDefaultPlayerCount = GameConfig.PracticeRoomDefaultPlayerCount,
        BaseProductionPerInterval = GameConfig.BaseProductionPerInterval,
        ProductionIntervalSeconds = GameConfig.ProductionIntervalSeconds,
        MovementDurationSeconds = GameConfig.MovementDurationSeconds,
        LobbyFillTimeoutSeconds = GameConfig.LobbyFillTimeoutSeconds,
        PracticeLobbyFillTimeoutSeconds = GameConfig.PracticeLobbyFillTimeoutSeconds,
        LobbyCountdownSeconds = GameConfig.LobbyCountdownSeconds,
        AbandonmentTimeoutSeconds = GameConfig.AbandonmentTimeoutSeconds,
        ResultScreenDurationSeconds = GameConfig.ResultScreenDurationSeconds,
        CommissionRate = commissionRate.ToString(CultureInfo.InvariantCulture)
    };
}
