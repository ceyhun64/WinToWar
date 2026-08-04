namespace api.Models.Payments;

/// <summary>
/// Bölüm 1.9: oyuncunun tüm maç geçmişinden bağımsız, tek doğruluk kaynağı olan
/// güncel USD bakiyesi. Auth modülü henüz netleşmediğinden (docs/07-pages.md
/// "Auth mekanizmasının kendisi... ayrı bir görevde netleştirilmeli") 🛠️
/// <see cref="PlayerId"/>, client tarafından üretilip kalıcı olarak saklanan
/// (localStorage) bir kimliktir — gerçek auth eklendiğinde bu alan doğrudan o
/// kullanıcı id'sine karşılık gelecek şekilde değişmeden kullanılabilir.
/// </summary>
public class Wallet
{
    public required string PlayerId { get; init; }

    /// <summary>Her zaman &gt;= 0 — her azaltma işleminde WalletService guard uygular.</summary>
    public decimal BalanceUsd { get; set; }

    /// <summary>
    /// Oyuncunun "dosyadaki" LTC ödül adresi — bir oyuncu bir odaya doğrudan mevcut
    /// bakiyesinden katıldığında (hiç PaymentInvoice oluşmadan, bkz. RoomEntryService)
    /// kazanırsa ödülünün gönderileceği yerdir. İlk sağlandığında kaydedilir, sonraki
    /// katılımlarda tekrar sorulmadan kullanılır. PaymentInvoice.PayoutAddress'ten
    /// bağımsızdır (bir invoice varsa PayoutService önce onu tercih eder).
    /// </summary>
    public string? PayoutAddress { get; set; }
    public PayoutAddressFormat? PayoutAddressFormat { get; set; }
}
