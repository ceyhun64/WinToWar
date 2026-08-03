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

/// <summary>Bölüm 5.2 state machine.</summary>
public enum PayoutStatus
{
    PayoutPending,
    PayoutSent,
    Completed,
    Failed
}

/// <summary>Bölüm 5.3 state machine.</summary>
public enum RefundStatus
{
    RefundPending,
    RefundSent,
    Completed,
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
