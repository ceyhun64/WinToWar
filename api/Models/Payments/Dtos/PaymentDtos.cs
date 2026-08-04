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

    /// <summary>Bölüm 1.9: MatchId dolu invoice'larda, onay anında oyuncuyu lobiye rezerve edebilmek için gereklidir.</summary>
    public required string PlayerName { get; init; }

    public required string PayoutAddress { get; init; }
}

public class CreateTopUpInvoiceRequest
{
    public required string PlayerId { get; init; }
    public required decimal AmountUsd { get; init; }
    public required string PayoutAddress { get; init; }
}

public class PaymentInvoiceDto
{
    public required string InvoiceId { get; init; }

    /// <summary>null ise genel bakiye yükleme (top-up) invoice'ıdır (bkz. Bölüm 1.9).</summary>
    public string? MatchId { get; init; }

    public required string PlayerId { get; init; }
    public required string Status { get; init; }
    public required string AmountUsd { get; init; }
    public required string AmountLtc { get; init; }
    public required string LockedUsdPerLtc { get; init; }
    public required string ReceivingAddress { get; init; }
    public required string Bip21Uri { get; init; }
    public required string ExpiresAt { get; init; }
    public required bool RateServedFromCache { get; init; }

    /// <summary>Bölüm 1.9 "Lobi dolma yarış durumu" — `/odeme/[invoiceId]` bu alanı polling ile okur.</summary>
    public required string MatchJoinOutcome { get; init; }
}

public class WalletDto
{
    public required string PlayerId { get; init; }
    public required string BalanceUsd { get; init; }
}

public class RequestWithdrawalRequest
{
    public required string PlayerId { get; init; }
    public required decimal AmountUsd { get; init; }
    public required string DestinationLtcAddress { get; init; }
}

public class WithdrawalRequestDto
{
    public required string Id { get; init; }
    public required string PlayerId { get; init; }
    public required string AmountUsd { get; init; }
    public required string AmountLtc { get; init; }
    public required string DestinationLtcAddress { get; init; }
    public required string Status { get; init; }
    public required string CreatedAt { get; init; }
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

/// <summary>
/// v8: tekil kazanan alanları yerine Recipients dizisi taşır (bkz. docs/05-payment.md
/// Bölüm 7) — Match.Winners tek elemanlıysa dizi de tek elemanlıdır, birden fazla
/// ortak kazanan varsa dizi o kadar eleman içerir.
/// </summary>
public class PayoutRecipientDto
{
    public required string WinnerPlayerId { get; init; }
    public required string PayoutAddress { get; init; }
    public required string AmountLtc { get; init; }
    public required string Status { get; init; }
    public string? BtcPayTransactionId { get; init; }
}

/// <summary>SignalR "PayoutCompleted" event payload'ı (v8: Recipients dizisiyle).</summary>
public class PayoutCompletedEvent
{
    public required string MatchId { get; init; }
    public required List<PayoutRecipientDto> Recipients { get; init; }
}

/// <summary>docs/07-pages.md `/mac/[matchId]`: bir maçın ödül/kazanç özeti.</summary>
public class PayoutSummaryDto
{
    public required string MatchId { get; init; }
    public required string Status { get; init; }
    public required string TotalPoolLtc { get; init; }
    public required string CommissionLtc { get; init; }
    public required List<PayoutRecipientDto> Recipients { get; init; }
}

/// <summary>SignalR "RefundCompleted" event payload'ı. MatchId null ise saf top-up invoice'ının iadesidir.</summary>
public class RefundCompletedEvent
{
    public string? MatchId { get; init; }
    public required string PlayerId { get; init; }
    public required string AmountLtc { get; init; }
    public required string Reason { get; init; }
}
