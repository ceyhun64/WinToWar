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
    ///
    /// 🐞 docs/21-payment-sandbox-e2e.md Aşama 4 (Bölüm 6, adım 7) gerçek regtest
    /// bulgusu: tek başına rank karşılaştırması YETERSİZDİ. Expired/Refunded/Failed
    /// aynı rank'i (2) paylaştığı için, ödemesi FİİLEN alınmış ve bakiyeye
    /// kredilenmiş bir `Confirmed` invoice'a geç kalmış (out-of-order) bir
    /// `InvoiceExpired`/`InvoiceInvalid` webhook'u geldiğinde geçiş kabul ediliyor
    /// ve invoice `Expired`/`Failed` olarak işaretleniyordu — sandbox'ta fiilen
    /// üretildi. Bu, Bölüm 5.1'deki state machine'e aykırıdır: oradaki diyagramda
    /// Expired/Failed dalları YALNIZCA `Pending`'den çıkar; `Confirmed`'den çıkan
    /// tek geçiş `Refunded`'dır (RefundService.SubmitAndPersistAsync bunu kullanır).
    /// Sonucu para kaybı değil ama gerçek parayla ödenmiş bir kaydın "başarısız"
    /// olarak raporlanmasıydı (PaymentService.GetFailedInvoicesAsync → /admin/odemeler
    /// ve oyuncunun /gecmis listesi). Rank monotonluğu korunur, üzerine Bölüm 5.1'in
    /// izin verdiği geçişler kısıtı eklenir.
    /// </summary>
    public static bool IsForwardTransition(PaymentInvoiceStatus current, PaymentInvoiceStatus incoming)
    {
        if (GetRank(incoming) <= GetRank(current))
        {
            return false;
        }

        // Bölüm 5.1: Confirmed'den yalnızca Refunded'a geçilir. (Pending ve
        // Confirmed dışındaki tüm state'ler terminaldir ve zaten yukarıdaki rank
        // kontrolüne takılır, bu yüzden başka bir `current` değeri kalmaz.)
        if (current == PaymentInvoiceStatus.Confirmed && incoming != PaymentInvoiceStatus.Refunded)
        {
            return false;
        }

        return true;
    }
}
