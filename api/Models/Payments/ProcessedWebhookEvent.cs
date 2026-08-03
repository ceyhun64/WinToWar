namespace api.Models.Payments;

/// <summary>
/// Bölüm 8.4: webhook idempotency tablosu. Bölüm 5.4'teki monotonluk kontrolüyle
/// birlikte çalışır — reddedilen (geriye dönük) bir webhook de burada kaydedilir,
/// böylece aynı event tekrar geldiğinde ikinci kez işlenmeye çalışılmaz.
/// </summary>
public class ProcessedWebhookEvent
{
    /// <summary>BTCPay event id'si — PK, doğal idempotency anahtarı.</summary>
    public required string EventId { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }

    public Guid? PaymentInvoiceId { get; set; }
}
