using api.Services.Payments;

namespace api.Tests;

/// <summary>Bölüm 2.3: yuvarlama yalnızca kalıcılaştırma sınırında, tek seferde yapılır.</summary>
public class PaymentMathTests
{
    [Fact]
    public void RoundForPersistence_UsesEightDecimalsWithToEvenMidpointRounding()
    {
        // Tam midpoint durumları: ToEven (banker's rounding) en yakın çift basamağa yuvarlar.
        Assert.Equal(0.00000002m, PaymentMath.RoundForPersistence(0.000000015m)); // 1 tek, 2 çift -> 2
        Assert.Equal(0.00000004m, PaymentMath.RoundForPersistence(0.000000045m)); // 4 zaten çift -> 4
        Assert.Equal(0.12345678m, PaymentMath.RoundForPersistence(0.123456785m)); // 8 zaten çift -> 8
    }

    [Fact]
    public void RoundUsdForPersistence_UsesTwoDecimalsWithToEvenMidpointRounding()
    {
        Assert.Equal(1.00m, PaymentMath.RoundUsdForPersistence(1.005m)); // 0 çift -> 1.00
        Assert.Equal(1.02m, PaymentMath.RoundUsdForPersistence(1.015m)); // 2 çift -> 1.02
    }

    [Fact]
    public void MultiStepPayoutCalculation_DoesNotAccumulateRoundingError_UntilFinalPersistenceRound()
    {
        // Bölüm 9 test senaryosu: TotalPoolLtc -> CommissionLtc -> NetworkFeeLtc -> AmountLtc
        // zincirinde ara adımlarda Round çağrılmaz; yalnızca son persist adımında yuvarlanır.
        const decimal totalPoolLtc = 0.044953921234m; // bilinçli olarak 8 basamaktan uzun bir ara değer
        const decimal commissionRate = 0.10m;
        const decimal estimatedFeeLtc = 0.0005m;

        var commissionLtc = PaymentMath.CalculateCommission(totalPoolLtc, commissionRate);
        var amountLtc = PaymentMath.CalculatePerWinnerShareLtc(totalPoolLtc, commissionLtc, winnerCount: 1, estimatedFeeLtc);

        // Ara değerler tam hassasiyetle taşınmış olmalı (decimal'in doğal hassasiyeti korunur,
        // 8 basamaktan fazla ondalık içerebilir — henüz hiç yuvarlanmadı).
        Assert.Equal(0.0044953921234m, commissionLtc);
        Assert.Equal(totalPoolLtc - commissionLtc - estimatedFeeLtc, amountLtc);

        // Yalnızca burada, kalıcılaştırma sınırında, TEK SEFERDE yuvarlanır.
        var roundedTotal = PaymentMath.RoundForPersistence(totalPoolLtc);
        var roundedCommission = PaymentMath.RoundForPersistence(commissionLtc);
        var roundedAmount = PaymentMath.RoundForPersistence(amountLtc);

        Assert.Equal(0.04495392m, roundedTotal);
        Assert.Equal(0.00449539m, roundedCommission);
        Assert.Equal(0.03995853m, roundedAmount);
    }

    [Fact]
    public void CalculateAmountLtc_DividesUsdByLockedRate()
    {
        var result = PaymentMath.CalculateAmountLtc(1.00m, 44.49m);
        Assert.Equal(PaymentMath.RoundForPersistence(1.00m / 44.49m), PaymentMath.RoundForPersistence(result));
    }
}
