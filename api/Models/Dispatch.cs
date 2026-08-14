namespace api.Models;

/// <summary>
/// docs/19-army.md: bir `AttackRegion` çağrısının, kaynak bölgeden gerçek zamana yayılı
/// olarak ayrılan tek bir sevkiyatı. Bir bölgeden aynı anda birden fazla `Dispatch` aktif
/// olabilir (farklı hedeflere, docs/03-game-rules.md Bölüm 6 güncel karar) — her biri kendi
/// `TotalAmount`/`SpawnedCount`'unu ayrı tutar, aynı asker iki dispatch'e birden atanamaz
/// (bkz. MovementService.StartDispatch'teki "available" hesaplaması).
///
/// `TotalAmount - SpawnedCount`, o an region'da fiziksel olarak hâlâ duran ama bu dispatch'e
/// "rezerve edilmiş" (başka bir dispatch'e atanamaz) asker sayısıdır — region.SoldierCount
/// bu miktarı her zaman İÇERİR (henüz ayrılmadıkları için fiziksel olarak hâlâ oradadırlar),
/// yalnızca yeni bir dispatch başlatılırken "kullanılabilir" hesabından düşülür.
/// </summary>
public class Dispatch
{
    public required string Id { get; init; }
    public required string OwnerId { get; init; }
    public required string FromRegionId { get; init; }
    public required string ToRegionId { get; init; }
    public required int TotalAmount { get; init; }
    public int SpawnedCount { get; set; }
    public required DateTime StartedAtUtc { get; init; }

    /// <summary>docs/03-game-rules.md Bölüm 10 tie-break kuralıyla aynı prensip — bkz. Army.SequenceNo.</summary>
    public required long SequenceNo { get; init; }
}
