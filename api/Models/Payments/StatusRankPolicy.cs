namespace api.Models.Payments;

/// <summary>
/// Bölüm 5.4 monotonluk kuralını somutlaştırır: her state machine için sabit,
/// artan bir <c>StatusRank</c> sıralaması. Terminal state'ler (Expired/Refunded/
/// Failed için invoice; Completed için payout/refund) en yüksek rank'e sahiptir
/// ki ulaşıldıktan sonra hiçbir geçiş kabul edilmesin.
///
/// 🛠️ Kapsam netleştirmesi: bu guard, dıştan (BTCPay webhook'u) sırasız gelen
/// event'lere karşı <see cref="PaymentInvoiceStatus"/> için uygulanır (Bölüm 3.1,
/// 5.4, 9. Test senaryoları — hepsi invoice/webhook bağlamında). Payout/Refund
/// state geçişleri ise webhook'tan değil, PayoutService/RefundService'in kendi
/// retry döngüsünden (dahili, otoriter kontrol akışı) kaynaklanır; retry akışında
/// Failed → PayoutPending/PayoutSent gibi "geriye" görünen ama aslında yeni bir
/// deneme başlatan geçişler kasıtlıdır ve StatusRank guard'ına tabi değildir.
/// Buna karşın reconciliation'ın PayoutSent → Completed geçişi zaten kendi
/// doğal guard'ıyla (yalnızca PayoutSent'ten Completed'a) korunur.
/// </summary>
public static class StatusRankPolicy
{
    private static readonly Dictionary<PaymentInvoiceStatus, int> InvoiceRanks = new()
    {
        [PaymentInvoiceStatus.Pending] = 0,
        [PaymentInvoiceStatus.Confirmed] = 1,
        [PaymentInvoiceStatus.Expired] = 2,
        [PaymentInvoiceStatus.Refunded] = 2,
        [PaymentInvoiceStatus.Failed] = 2
    };

    private static readonly HashSet<PaymentInvoiceStatus> TerminalInvoiceStatuses = new()
    {
        PaymentInvoiceStatus.Expired,
        PaymentInvoiceStatus.Refunded,
        PaymentInvoiceStatus.Failed
    };

    public static int GetRank(PaymentInvoiceStatus status) => InvoiceRanks[status];

    public static bool IsTerminal(PaymentInvoiceStatus status) => TerminalInvoiceStatuses.Contains(status);

    /// <summary>
    /// Bir webhook'un bildirdiği <paramref name="incoming"/> state'in mevcut
    /// <paramref name="current"/> state'e uygulanıp uygulanamayacağını belirler.
    /// Yalnızca mevcut rank'ten daha yüksek bir rank'e geçiş kabul edilir.
    /// </summary>
    public static bool IsForwardTransition(PaymentInvoiceStatus current, PaymentInvoiceStatus incoming) =>
        GetRank(incoming) > GetRank(current);

    private static readonly Dictionary<PayoutStatus, int> PayoutRanks = new()
    {
        [PayoutStatus.PayoutPending] = 0,
        [PayoutStatus.PayoutSent] = 1,
        [PayoutStatus.Completed] = 2,
        [PayoutStatus.Failed] = -1 // Terminal değil; retry ile geri dönebilir, guard dışı.
    };

    public static int GetRank(PayoutStatus status) => PayoutRanks[status];

    private static readonly Dictionary<RefundStatus, int> RefundRanks = new()
    {
        [RefundStatus.RefundPending] = 0,
        [RefundStatus.RefundSent] = 1,
        [RefundStatus.Completed] = 2,
        [RefundStatus.Failed] = -1 // Terminal değil; retry ile geri dönebilir, guard dışı.
    };

    public static int GetRank(RefundStatus status) => RefundRanks[status];
}
