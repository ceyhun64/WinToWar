namespace api.Models.Payments;

/// <summary>
/// Bölüm 2.2 (v8): kazanan başına bir satır (Payout ile 1-N, N=1 normal senaryoda).
/// Bu kazanana özel durum — bir kazanana giden işlem Failed/retry döngüsündeyken
/// diğerleri bundan etkilenmez, her biri kendi ayrı BTCPay işlemine sahiptir.
/// Unique constraint: (PayoutId, WinnerPlayerId) — aynı kazanan için aynı Payout'a
/// iki kez satır yazılamaz (idempotency, ikinci katman).
/// </summary>
public class PayoutRecipient
{
    public Guid Id { get; set; }
    public Guid PayoutId { get; set; }
    public required string WinnerPlayerId { get; set; }

    /// <summary>Kazananın PaymentInvoice.PayoutAddress'inden aynen kopyalanır, bu satırda da değiştirilemez.</summary>
    public required string PayoutAddress { get; set; }

    /// <summary>Bu kazanana fiilen gönderilen tutar (tahmini fee ile hesaplanıp gönderilir, geriye dönük değişmez).</summary>
    public decimal AmountLtc { get; set; }

    /// <summary>
    /// 🔒 Bölüm 2.6: null olarak başlar; yalnızca BTCPay'in bu kazanana ait payout'u
    /// tamamlayıp raporladığı gerçek (actual) fee ile bir kez doldurulur. Tahmini
    /// fee asla buraya yazılmaz.
    /// </summary>
    public decimal? NetworkFeeLtc { get; set; }

    public PayoutStatus Status { get; set; } = PayoutStatus.PayoutPending;

    public string? BtcPayTransactionId { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
