namespace api.Models;

public enum GeneralStatus
{
    Garrisoned,
    Moving,
    Dead
}

/// <summary>
/// General Porsuk. Ordunun saldırabilmesi için en az bir tane hayatta ve
/// bir bölgede garnizon halinde olması gerekir. Ölünce RespawnAtUtc alanı,
/// GameConfig.GeneralRespawnTimeSeconds kadar sonrasına ayarlanır.
/// </summary>
public class General
{
    public required string Id { get; init; }
    public required string OwnerId { get; init; }
    public GeneralStatus Status { get; set; } = GeneralStatus.Garrisoned;
    public string? CurrentRegionId { get; set; }
    public DateTime? RespawnAtUtc { get; set; }
}
