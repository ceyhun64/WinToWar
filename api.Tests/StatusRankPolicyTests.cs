using api.Models.Payments;

namespace api.Tests;

/// <summary>Bölüm 5.4: monotonluk kuralı — webhook'lar sırasız gelse dahi state asla geriye alınmaz.</summary>
public class StatusRankPolicyTests
{
    [Fact]
    public void Pending_To_Confirmed_IsForwardTransition()
    {
        Assert.True(StatusRankPolicy.IsForwardTransition(PaymentInvoiceStatus.Pending, PaymentInvoiceStatus.Confirmed));
    }

    [Fact]
    public void Confirmed_To_Pending_IsRejected()
    {
        Assert.False(StatusRankPolicy.IsForwardTransition(PaymentInvoiceStatus.Confirmed, PaymentInvoiceStatus.Pending));
    }

    [Fact]
    public void Confirmed_To_Confirmed_IsRejected_SameRankIsNotForward()
    {
        // Bölüm 9 test senaryosu: aynı rank'e (ör. gecikmeli bir ikinci "Confirmed" event'i) geçiş kabul edilmez.
        Assert.False(StatusRankPolicy.IsForwardTransition(PaymentInvoiceStatus.Confirmed, PaymentInvoiceStatus.Confirmed));
    }

    [Fact]
    public void Pending_To_Confirmed_Then_LateArriving_LowerRank_IsIgnored()
    {
        // "önce Confirmed'e geçiren event işlenir, ardından daha düşük rank'li bir event gelir" senaryosu.
        var current = PaymentInvoiceStatus.Confirmed;
        var lateArriving = PaymentInvoiceStatus.Pending;

        Assert.False(StatusRankPolicy.IsForwardTransition(current, lateArriving));
    }

    [Theory]
    [InlineData(PaymentInvoiceStatus.Expired)]
    [InlineData(PaymentInvoiceStatus.Refunded)]
    [InlineData(PaymentInvoiceStatus.Failed)]
    public void TerminalState_BlocksAllFurtherTransitions(PaymentInvoiceStatus terminal)
    {
        Assert.True(StatusRankPolicy.IsTerminal(terminal));

        foreach (var candidate in Enum.GetValues<PaymentInvoiceStatus>())
        {
            Assert.False(StatusRankPolicy.IsForwardTransition(terminal, candidate));
        }
    }

    [Fact]
    public void Confirmed_To_Refunded_IsForwardTransition()
    {
        // Bölüm 1.6/2.6: onaylanmış bir invoice sonradan (ör. eşleşme bulunamadı) refund edilebilir.
        Assert.True(StatusRankPolicy.IsForwardTransition(PaymentInvoiceStatus.Confirmed, PaymentInvoiceStatus.Refunded));
    }

    /// <summary>
    /// 🐞 Regresyon — docs/21-payment-sandbox-e2e.md Aşama 4 (Bölüm 6 adım 7) gerçek
    /// regtest bulgusu: ödemesi alınıp bakiyeye kredilenmiş bir `Confirmed` invoice'a
    /// geç kalmış bir `InvoiceExpired`/`InvoiceInvalid` webhook'u geldiğinde, geçiş
    /// (Expired/Failed rank'i Confirmed'den yüksek olduğu için) kabul ediliyordu ve
    /// gerçekten ödenmiş bir kayıt "başarısız" olarak raporlanıyordu. Bölüm 5.1'de
    /// Expired/Failed dalları yalnızca Pending'den çıkar.
    /// </summary>
    [Theory]
    [InlineData(PaymentInvoiceStatus.Expired)]
    [InlineData(PaymentInvoiceStatus.Failed)]
    public void Confirmed_To_ExpiredOrFailed_IsRejected(PaymentInvoiceStatus lateArriving)
    {
        Assert.False(StatusRankPolicy.IsForwardTransition(PaymentInvoiceStatus.Confirmed, lateArriving));
    }

    [Theory]
    [InlineData(PaymentInvoiceStatus.Confirmed)]
    [InlineData(PaymentInvoiceStatus.Expired)]
    [InlineData(PaymentInvoiceStatus.Refunded)]
    [InlineData(PaymentInvoiceStatus.Failed)]
    public void Pending_To_AnyDefinedNextState_IsForwardTransition(PaymentInvoiceStatus incoming)
    {
        // Bölüm 5.1: Pending'den dört geçişin dördü de geçerlidir — yukarıdaki
        // kısıt yalnızca Confirmed'i etkilemeli, Pending akışını daraltmamalı.
        Assert.True(StatusRankPolicy.IsForwardTransition(PaymentInvoiceStatus.Pending, incoming));
    }
}
