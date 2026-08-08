namespace api.Models.Payments;

/// <summary>
/// Bölüm 2.1. Oyun motoru domain modelinden (api.Models) tamamen ayrı bir
/// persistence katmanında yaşar (bkz. PaymentDbContext). <see cref="PlayerId"/>
/// ve <see cref="MatchId"/>, oyun motorundaki Player.Id/Match.Id (Guid-biçimli
/// string) değerlerine mantıksal olarak karşılık gelir; oyun state'i bellekte
/// tutulduğundan (MatchManager) burada gerçek bir DB-level foreign key kurulamaz
/// — 🛠️ bu nedenle düz string olarak saklanır.
/// </summary>
public class PaymentInvoice
{
    public Guid Id { get; set; }
    public required string PlayerId { get; set; }

    /// <summary>🛠️ v9 (Bölüm 1.9): null ise genel bakiye yükleme (top-up); dolu ise maça giriş/top-up-ve-katıl invoice'ı.</summary>
    public string? MatchId { get; set; }

    /// <summary>🛠️ MatchId dolu olduğunda, onay anında (henüz lobiye eklenmemiş) oyuncuyu rezerve edebilmek için gereklidir.</summary>
    public string? PlayerName { get; set; }

    /// <summary>Bölüm 1.9 "Lobi dolma yarış durumu" — yalnızca MatchId dolu invoice'larda anlamlıdır.</summary>
    public MatchJoinOutcome MatchJoinOutcome { get; set; } = MatchJoinOutcome.NotApplicable;

    /// <summary>İdempotency anahtarı — BTCPay tarafındaki invoice id'si.</summary>
    public required string BtcPayInvoiceId { get; set; }

    /// <summary>
    /// Provider'ın (BTCPay/Fake) ödeme oluştururken döndürdüğü alım adresi/BIP-21
    /// URI'si — invoice oluşturulduğu anda kalıcılaştırılır ki her GET/polling
    /// isteğinde (ör. `/odeme/[invoiceId]`) tekrar tekrar provider'a sormaya
    /// gerek kalmasın ve adres asla boş dönmesin.
    /// </summary>
    public required string ReceivingAddress { get; set; }
    public required string Bip21Uri { get; set; }

    public decimal AmountUsd { get; set; }

    /// <summary>Kalıcılaştırma anında PaymentMath.RoundForPersistence ile yuvarlanmış nihai değer.</summary>
    public decimal AmountLtc { get; set; }

    public decimal LockedUsdPerLtc { get; set; }

    public PriceOracleSource PriceOracleSource { get; set; }
    public bool RateServedFromCache { get; set; }
    public int RateAgeSecondsAtUse { get; set; }

    public PaymentInvoiceStatus Status { get; set; } = PaymentInvoiceStatus.Pending;

    /// <summary>
    /// Bölüm 2.1 "yeni — `/odeme/[invoiceId]` UI'ının canlı onay ilerlemesi
    /// göstermesi için eklendi": webhook her onay geldiğinde günceller. Yalnızca
    /// gösterim amaçlıdır, hiçbir hesaplamada kullanılmaz (asıl eşik kontrolü
    /// Bölüm 1.4'teki RequiredConfirmations ile PaymentService'te ayrıca yapılır).
    /// </summary>
    public int CurrentConfirmations { get; set; }

    /// <summary>Bölüm 2.1: computed/mapped — burada Status'tan türetilen, DB'ye yazılmayan bir alan.</summary>
    public int StatusRank => StatusRankPolicy.GetRank(Status);

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
}
