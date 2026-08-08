namespace api.Models.Payments;

/// <summary>
/// Bölüm 5.4 monotonluk kuralını somutlaştırır: <see cref="PaymentInvoiceStatus"/>
/// için sabit, artan bir <c>StatusRank</c> sıralaması. Terminal state'ler
/// (Expired/Refunded/Failed) en yüksek rank'e sahiptir ki ulaşıldıktan sonra
/// hiçbir geçiş kabul edilmesin. Bu guard, dıştan (BTCPay webhook'u) sırasız
/// gelen event'lere karşı uygulanır (Bölüm 3.1, 5.4) — Payout/Refund artık
/// senkron birer Wallet.BalanceUsd kredisi olduğundan (2026-08-08 kararı) kendi
/// ayrı bir state machine'e sahip değildir, bu guard'a ihtiyaç duymaz.
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
}
