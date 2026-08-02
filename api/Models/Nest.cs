namespace api.Models;

/// <summary>
/// Bir oyuncunun sahip olduğu bölgedeki Porsuk Yuvası / Kale yapısı.
/// SoldierAccumulator, dakikalık üretim oranının saniyelik tick'lere bölünmesinden
/// doğan kesirli birikimi tutar (bkz. GameConfig, EconomyTickService).
/// </summary>
public class Nest
{
    public required string RegionId { get; init; }
    public required string OwnerId { get; set; }
    public int Level { get; set; } = 1;
    public int GarrisonSoldiers { get; set; }
    public int GarrisonArchers { get; set; }
    public double SoldierAccumulator { get; set; }
}
