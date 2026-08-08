namespace api.Models;

/// <summary>
/// Bir bölgeden doğrudan komşu bir bölgeye hareket eden asker birliği. General
/// kavramı WinToWar'da yoktur (bkz. docs/03-game-rules.md Bölüm 5) — hareket
/// doğrudan askerle yapılır. Her gönderim tek hop'tur (sürükle-bırak, Bölüm 6/15);
/// bölge ele geçirildiğinde hayatta kalan tüm askerler o bölgede garrison olarak
/// kalır, otomatik zincirleme devam etmez.
/// </summary>
public class Army
{
    public required string Id { get; init; }
    public required string OwnerId { get; init; }
    public int SoldierCount { get; set; }
    public required string FromRegionId { get; init; }
    public required string ToRegionId { get; init; }

    public DateTime DepartedAtUtc { get; init; }

    /// <summary>Set erişimcisi (init değil) kasıtlı: MovementService varış zamanını hesaplarken kullanır.</summary>
    public DateTime ArrivesAtUtc { get; set; }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 10 "Tam eşzamanlı varış — tie-break kuralı":
    /// maç içi, `Match.NextArmySequenceNo`'dan atanan, hiçbir zaman azalmayan bir
    /// sayaç — `ArrivesAtUtc` milisaniye seviyesinde birebir eşit olan orduların
    /// hangisinin ÖNCE oluşturulduğunu (yola çıktığını) gösterir. `Id` (rastgele
    /// GUID) kasıtlı olarak tie-break için KULLANILMAZ — oluşturulma sırasıyla
    /// hiçbir ilişkisi yoktur.
    /// </summary>
    public required long SequenceNo { get; init; }
}
