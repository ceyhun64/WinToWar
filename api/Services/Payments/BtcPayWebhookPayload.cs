using System.Text.Json.Serialization;

namespace api.Services.Payments;

/// <summary>
/// BTCPay Greenfield webhook event payload'ının işlediğimiz alt kümesi.
/// <see cref="DeliveryId"/>, BTCPay'in her webhook teslimatına verdiği benzersiz
/// id'dir — Bölüm 8.4'teki idempotency anahtarı olarak kullanılır.
/// </summary>
public class BtcPayWebhookPayload
{
    [JsonPropertyName("deliveryId")]
    public required string DeliveryId { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    [JsonPropertyName("invoiceId")]
    public required string InvoiceId { get; init; }

    [JsonPropertyName("confirmations")]
    public int Confirmations { get; init; }

    /// <summary>Ödenen tutar (LTC), invariant culture string olarak — Bölüm 0.3.</summary>
    [JsonPropertyName("paidAmountLtc")]
    public string? PaidAmountLtc { get; init; }
}

/// <summary>BTCPay'in webhook event type alanının bu modülün ilgilendiği değerleri.</summary>
public static class BtcPayWebhookEventTypes
{
    public const string InvoiceSettled = "InvoiceSettled";
    public const string InvoiceProcessing = "InvoiceProcessing";
    public const string InvoiceExpired = "InvoiceExpired";
    public const string InvoiceInvalid = "InvoiceInvalid";
}
