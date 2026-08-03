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
}
