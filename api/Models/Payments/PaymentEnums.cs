namespace api.Models.Payments;

/// <summary>Bölüm 5.1 state machine.</summary>
public enum PaymentInvoiceStatus
{
    Pending,
    Confirmed,
    Expired,
    Refunded,
    Failed
}

public enum PayoutAddressFormat
{
    Base58Check,
    Bech32
}

/// <summary>
/// 🛠️ Bölüm 1.2: yalnızca gerçek sağlayıcı adını taşır. "Cache" bu enum'da
/// kasıtlı olarak YOKTUR — cache bir kaynak değil, bir teslimat yöntemidir
/// (bkz. PaymentInvoice.RateServedFromCache).
/// </summary>
public enum PriceOracleSource
{
    CoinGecko,
    CoinCap
}

/// <summary>Bölüm 2.2.1: refund'un tetiklenme nedeni.</summary>
public enum RefundReason
{
    MatchmakingTimeout,
    Overpayment,
    Manual
}

/// <summary>Bölüm 1.9: WithdrawalRequest.Status — tek gerçek async (on-chain gönderim gerektiren) para hareketi bu olduğundan kendi ayrı state machine'ini korur.</summary>
public enum WithdrawalRequestStatus
{
    Pending,
    Approved,
    Sent,
    Completed,
    Rejected,
    Failed
}

/// <summary>
/// 🚩❓ Bölüm 10.1 "KYC/AML zorunluluğu": üçüncü parti bir KYC sağlayıcı entegrasyonu
/// şimdi yazılmaz — bu yalnızca ileride doldurulabilecek bir placeholder alandır
/// (bkz. Wallet.KycStatus). Varsayılan her zaman NotRequired'dır.
/// </summary>
public enum KycStatus
{
    NotRequired,
    Pending,
    Verified,
    Rejected
}

/// <summary>
/// Bölüm 1.9 "Lobi dolma yarış durumu": MatchId dolu bir invoice onaylandığında,
/// oyuncunun fiilen o maça eklenip eklenemediğini `/odeme/[invoiceId]` sayfasının
/// polling ile okuyabilmesi için (bkz. docs/07-pages.md — bu sayfa SignalR değil
/// polling/webhook-tetiklemeli olarak tasarlanmıştır).
/// </summary>
public enum MatchJoinOutcome
{
    /// <summary>MatchId null (saf top-up) — hiç uygulanmaz.</summary>
    NotApplicable,
    Pending,
    Joined,
    RoomFull
}
