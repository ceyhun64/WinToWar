namespace api.Models;

/// <summary>
/// Bir maç içindeki bölgenin çalışma zamanı (runtime) durumu. WinToWar'da her bölge
/// ya bir oyuncunun rastgele atanmış "ev/başlangıç kalesi" (OriginalOwnerId dolu),
/// ya da hiçbir zaman bir evi olmayan gri/nötr bir bölgedir (OriginalOwnerId null —
/// bkz. docs/03-game-rules.md Bölüm 3). SoldierCount tek doğruluk kaynağıdır: hem
/// bir bölgenin savunma gücünü hem de (sahiplenilmişse) saldırı için gönderilebilir
/// asker havuzunu temsil eder.
/// </summary>
public class Region
{
    public required string Id { get; init; }

    /// <summary>Bu bölge hiçbir zaman bir oyuncunun ev kalesi olmadıysa null (daima gri kalır).</summary>
    public string? OriginalOwnerId { get; init; }

    /// <summary>Güncel sahip; hiç ele geçirilmemiş/kimsenin elinde değilse null.</summary>
    public string? OwnerId { get; set; }

    public int SoldierCount { get; set; }
}
