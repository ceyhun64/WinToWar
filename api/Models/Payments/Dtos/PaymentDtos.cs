namespace api.Models.Payments.Dtos;

/// <summary>
/// Bölüm 2.5, 7: SignalR/REST üzerinden client'a giden DTO'lar. Parasal alanlar
/// (Bölüm 2.5) açıkça <c>string</c> tutulur — decimal'in JSON serileştirmesinde
/// culture/format belirsizliğine düşmemek için. web/lib/game/types.ts içindeki
/// TypeScript tipleri bunlarla birebir eşleşir.
/// </summary>
public class CreatePaymentInvoiceRequest
{
    public required string PlayerId { get; init; }
    public required string PayoutAddress { get; init; }
}

public class PaymentInvoiceDto
{
    public required string InvoiceId { get; init; }
    public required string MatchId { get; init; }
    public required string PlayerId { get; init; }
    public required string Status { get; init; }
    public required string AmountUsd { get; init; }
    public required string AmountLtc { get; init; }
    public required string LockedUsdPerLtc { get; init; }
    public required string ReceivingAddress { get; init; }
    public required string Bip21Uri { get; init; }
    public required string ExpiresAt { get; init; }
    public required bool RateServedFromCache { get; init; }
}

public class PaymentErrorResponse
{
    public required string Code { get; init; }
    public required string Message { get; init; }
}

/// <summary>SignalR "PaymentConfirmed" event payload'ı.</summary>
public class PaymentConfirmedEvent
{
    public required string InvoiceId { get; init; }
    public required string MatchId { get; init; }
    public required string PlayerId { get; init; }
    public required string AmountLtc { get; init; }
}

/// <summary>SignalR "PayoutCompleted" event payload'ı.</summary>
public class PayoutCompletedEvent
{
    public required string MatchId { get; init; }
    public required string WinnerPlayerId { get; init; }
    public required string AmountLtc { get; init; }
}

/// <summary>SignalR "RefundCompleted" event payload'ı.</summary>
public class RefundCompletedEvent
{
    public required string MatchId { get; init; }
    public required string PlayerId { get; init; }
    public required string AmountLtc { get; init; }
    public required string Reason { get; init; }
}
