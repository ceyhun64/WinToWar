namespace api.Services.Payments;

/// <summary>
/// Bölüm 2.3 kuralını somutlaştırır: bir hesaplama zinciri birden fazla ara
/// adımdan oluşuyorsa, ara adımlarda ayrı ayrı <c>Round</c> çağrılmaz — decimal
/// tipi doğal hassasiyetiyle taşınır. Yuvarlama yalnızca değer veritabanına
/// yazılmadan hemen önce, <see cref="RoundForPersistence"/> ile bir kez uygulanır.
/// Bu kural istisnasızdır: tüm ödeme servisleri bu sınıf üzerinden yuvarlar.
/// </summary>
public static class PaymentMath
{
    private const int LtcDecimals = 8;
    private const int UsdDecimals = 2;

    /// <summary>LTC tutarını kalıcılaştırma sınırında 8 ondalığa yuvarlar (ToEven).</summary>
    public static decimal RoundForPersistence(decimal ltcValue) =>
        Math.Round(ltcValue, LtcDecimals, MidpointRounding.ToEven);

    /// <summary>USD tutarını kalıcılaştırma sınırında 2 ondalığa yuvarlar (ToEven).</summary>
    public static decimal RoundUsdForPersistence(decimal usdValue) =>
        Math.Round(usdValue, UsdDecimals, MidpointRounding.ToEven);

    /// <summary>AmountLtc = AmountUsd / LockedUsdPerLtc — yuvarlanmamış ara değer.</summary>
    public static decimal CalculateAmountLtc(decimal amountUsd, decimal lockedUsdPerLtc) =>
        amountUsd / lockedUsdPerLtc;

    /// <summary>CommissionLtc = TotalPoolLtc * CommissionRate — yuvarlanmamış ara değer.</summary>
    public static decimal CalculateCommission(decimal totalPoolLtc, decimal commissionRate) =>
        totalPoolLtc * commissionRate;

    /// <summary>
    /// Bölüm 2.2 (v8, N&gt;1 çoklu-kazanan formülü) / Bölüm 3.2 akış diyagramı:
    /// PerWinnerAmountLtc = (TotalPoolLtc - CommissionLtc) / WinnerCount - estimatedFee.
    /// N=1 (normal, tek kazanan) senaryoda winnerCount=1 ile çağrılır — genel akışın
    /// N=1 özel durumu, ayrı bir formül/dal değildir (PayoutService bunu tek yoldan uygular).
    /// </summary>
    public static decimal CalculatePerWinnerShareLtc(decimal totalPoolLtc, decimal commissionLtc, int winnerCount, decimal networkFeeLtc) =>
        (totalPoolLtc - commissionLtc) / winnerCount - networkFeeLtc;
}
