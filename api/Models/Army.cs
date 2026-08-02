namespace api.Models;

/// <summary>
/// Bir General önderliğinde, bir bölgeden diğerine hareket eden asker birliği.
/// Varış anında MovementService tarafından tespit edilip CombatService'e (hedef
/// düşman/nötrse) ya da doğrudan garnizona katılmaya (hedef kendi bölgesiyse) yönlendirilir.
/// </summary>
public class Army
{
    public required string Id { get; init; }
    public required string OwnerId { get; init; }
    public required string GeneralId { get; init; }
    public int SoldierCount { get; set; }
    public required string FromRegionId { get; init; }
    public required string ToRegionId { get; init; }
    public DateTime DepartedAtUtc { get; init; }

    /// <summary>Set erişimcisi (init değil) kasıtlı: MovementService varış zamanını hesaplarken kullanır.</summary>
    public DateTime ArrivesAtUtc { get; set; }
}
