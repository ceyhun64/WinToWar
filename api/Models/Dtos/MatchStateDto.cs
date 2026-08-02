namespace api.Models.Dtos;

/// <summary>
/// SignalR üzerinden client'a gönderilen maç durumu. Backend domain modeli
/// (Match, Player, Region, ...) doğrudan yayınlanmaz; her zaman bu DTO'lara
/// map'lenir. web/lib/game/types.ts içindeki TypeScript tipleri bunlarla
/// birebir eşleşmelidir.
/// </summary>
public class MatchStateDto
{
    public required string MatchId { get; init; }
    public required string Status { get; init; }
    public int RemainingSeconds { get; init; }
    public string? WinnerId { get; init; }
    public required List<PlayerDto> Players { get; init; }
    public required List<RegionStateDto> Regions { get; init; }
    public required List<GeneralDto> Generals { get; init; }
    public required List<ArmyDto> Armies { get; init; }
}

public class PlayerDto
{
    public required string Id { get; init; }
    public required int Slot { get; init; }
    public required string Name { get; init; }
    public required int Gold { get; init; }
    public required bool IsEliminated { get; init; }
    public required bool IsConnected { get; init; }
}

public class RegionStateDto
{
    public required string Id { get; init; }
    public string? OwnerId { get; init; }
    public int? NestLevel { get; init; }
    public int GarrisonSoldiers { get; init; }
    public int GarrisonArchers { get; init; }
    public int NeutralDefenseSoldiers { get; init; }
}

public class GeneralDto
{
    public required string Id { get; init; }
    public required string OwnerId { get; init; }
    public required string Status { get; init; }
    public string? CurrentRegionId { get; init; }
    public int? RespawnInSeconds { get; init; }
}

public class ArmyDto
{
    public required string Id { get; init; }
    public required string OwnerId { get; init; }
    public required string GeneralId { get; init; }
    public required int SoldierCount { get; init; }
    public required string FromRegionId { get; init; }
    public required string ToRegionId { get; init; }
    public required int ArrivesInSeconds { get; init; }
}
