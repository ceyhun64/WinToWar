namespace api.Models.Payments.Dtos;

/// <summary>
/// Bölüm 2.5, 7: SignalR/REST üzerinden client'a giden DTO'lar. Parasal alanlar
/// (Bölüm 2.5) açıkça <c>string</c> tutulur — decimal'in JSON serileştirmesinde
/// culture/format belirsizliğine düşmemek için. web/lib/game/types.ts içindeki
/// TypeScript tipleri bunlarla birebir eşleşir.
/// </summary>
/// <summary>
/// docs/11-auth.md Bölüm 0.4: PlayerId artık burada taşınmaz — controller çağıranın
/// JWT'sinden okur (bkz. PaymentsController/WalletController CurrentPlayerId).
/// </summary>
public class CreatePaymentInvoiceRequest
{
    /// <summary>Bölüm 1.9: MatchId dolu invoice'larda, onay anında oyuncuyu lobiye rezerve edebilmek için gereklidir.</summary>
    public required string PlayerName { get; init; }
}

public class CreateTopUpInvoiceRequest
{
    public required decimal AmountUsd { get; init; }
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
    public required string CreatedAt { get; init; }
    public required bool RateServedFromCache { get; init; }

    /// <summary>Bölüm 1.9 "Lobi dolma yarış durumu" — `/odeme/[invoiceId]` bu alanı polling ile okur.</summary>
    public required string MatchJoinOutcome { get; init; }

    /// <summary>
    /// Bölüm 2.1 / `07-pages.md` `/odeme/[invoiceId]`: canlı onay ilerlemesi
    /// ("1/2 onay" gibi) göstermek için — CurrentConfirmations webhook'larla artar,
    /// RequiredConfirmations sabit eşiktir (Bölüm 1.4, PaymentConfig).
    /// </summary>
    public required int CurrentConfirmations { get; init; }
    public required int RequiredConfirmations { get; init; }
}

public class WalletDto
{
    public required string PlayerId { get; init; }
    public required string BalanceUsd { get; init; }
}

public class RequestWithdrawalRequest
{
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
/// 2026-08-08 kararı: kazanç artık on-chain LTC olarak değil, doğrudan
/// Wallet.BalanceUsd'ye kredi olarak işlenir (bkz. PayoutService) — bu yüzden
/// adres/on-chain durum/tx id alanları taşımaz, yalnızca kredilenen USD tutarı.
/// Match.Winners tek elemanlıysa dizi de tek elemanlıdır, birden fazla ortak
/// kazanan varsa dizi o kadar eleman içerir.
/// </summary>
public class PayoutRecipientDto
{
    public required string WinnerPlayerId { get; init; }
    public required string AmountUsd { get; init; }
}

/// <summary>SignalR "PayoutCompleted" event payload'ı.</summary>
public class PayoutCompletedEvent
{
    public required string MatchId { get; init; }
    public required List<PayoutRecipientDto> Recipients { get; init; }
}

/// <summary>docs/07-pages.md `/mac/[matchId]`: bir maçın ödül/kazanç özeti.</summary>
public class PayoutSummaryDto
{
    public required string MatchId { get; init; }
    public required string TotalPoolUsd { get; init; }
    public required string CommissionUsd { get; init; }
    public required List<PayoutRecipientDto> Recipients { get; init; }
}
