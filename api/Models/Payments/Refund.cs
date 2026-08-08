namespace api.Models.Payments;

/// <summary>
/// 2026-08-08 kararı: iade artık on-chain LTC gönderimi değil, doğrudan
/// Wallet.BalanceUsd'ye kredi olarak işlenir (bkz. RefundService.SubmitAsync) —
/// bu yüzden bir hedef LTC adresine ihtiyaç duymaz, ayrı bir gönderim/retry adımı
/// da yoktur (kredi atomik ve anındadır).
/// </summary>
public class Refund
{
    public Guid Id { get; set; }

    /// <summary>İdempotency anahtarı — bir invoice en fazla bir kez refund edilir.</summary>
    public required Guid PaymentInvoiceId { get; set; }

    public required string PlayerId { get; set; }

    public decimal AmountUsd { get; set; }
    public RefundReason Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
