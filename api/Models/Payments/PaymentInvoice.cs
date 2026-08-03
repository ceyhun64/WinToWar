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
    public required string MatchId { get; set; }

    /// <summary>İdempotency anahtarı — BTCPay tarafındaki invoice id'si.</summary>
    public required string BtcPayInvoiceId { get; set; }

    public decimal AmountUsd { get; set; }

    /// <summary>Kalıcılaştırma anında PaymentMath.RoundForPersistence ile yuvarlanmış nihai değer.</summary>
    public decimal AmountLtc { get; set; }

    public decimal LockedUsdPerLtc { get; set; }

    public PriceOracleSource PriceOracleSource { get; set; }
    public bool RateServedFromCache { get; set; }
    public int RateAgeSecondsAtUse { get; set; }

    public required string PayoutAddress { get; set; }
    public PayoutAddressFormat PayoutAddressFormat { get; set; }

    public PaymentInvoiceStatus Status { get; set; } = PaymentInvoiceStatus.Pending;

    /// <summary>Bölüm 2.1: computed/mapped — burada Status'tan türetilen, DB'ye yazılmayan bir alan.</summary>
    public int StatusRank => StatusRankPolicy.GetRank(Status);

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
}
