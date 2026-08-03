namespace api.Models.Payments;

/// <summary>Bölüm 2.2.</summary>
public class Payout
{
    public Guid Id { get; set; }

    /// <summary>İdempotency anahtarı — bir maç için en fazla bir Payout satırı oluşur.</summary>
    public required string MatchId { get; set; }

    public required string WinnerPlayerId { get; set; }

    public decimal TotalPoolLtc { get; set; }
    public decimal CommissionLtc { get; set; }

    /// <summary>
    /// 🔒 Bölüm 2.6: null olarak başlar; yalnızca BTCPay'in payout'u tamamlayıp
    /// raporladığı gerçek (actual) fee ile bir kez doldurulur. Tahmini fee asla
    /// buraya yazılmaz.
    /// </summary>
    public decimal? NetworkFeeLtc { get; set; }

    /// <summary>Kazanana fiilen gönderilen tutar (tahmini fee ile hesaplanıp gönderilir, geriye dönük değişmez).</summary>
    public decimal AmountLtc { get; set; }

    public PayoutStatus Status { get; set; } = PayoutStatus.PayoutPending;

    public string? BtcPayTransactionId { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
