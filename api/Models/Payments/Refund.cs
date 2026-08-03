namespace api.Models.Payments;

/// <summary>Bölüm 2.2.1 — v6 şeması dokümanda verilmediği için (yalnızca "v6 ile aynı" notu var),
/// Bölüm 1.6/2.6/3.3 refund akışlarının ihtiyaç duyduğu minimal alan seti burada tanımlandı.</summary>
public class Refund
{
    public Guid Id { get; set; }

    /// <summary>İdempotency anahtarı — bir invoice en fazla bir kez refund edilir.</summary>
    public required Guid PaymentInvoiceId { get; set; }

    public required string PlayerId { get; set; }

    public decimal AmountLtc { get; set; }
    public RefundReason Reason { get; set; }

    public RefundStatus Status { get; set; } = RefundStatus.RefundPending;

    public string? BtcPayTransactionId { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
