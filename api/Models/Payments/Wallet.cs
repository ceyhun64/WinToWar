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
    /// 🚩❓ docs/05-payment.md Bölüm 10.1 "KYC/AML zorunluluğu": ileride doldurulabilecek
    /// placeholder — üçüncü parti bir KYC sağlayıcı entegrasyonu şimdi yazılmaz.
    /// Wallet üzerinde tutulur çünkü projede henüz ayrı, kalıcı bir User/Account
    /// entity'si yok (bkz. `PlayerId` yorumundaki auth notu) — Wallet, bir oyuncunun
    /// platformla süregelen ilişkisini temsil eden tek EF Core ile kalıcı kayıttır.
    /// </summary>
    public KycStatus KycStatus { get; set; } = KycStatus.NotRequired;

    /// <summary>
    /// docs/05-payment.md Bölüm 10.1: yaş onayının ne zaman verildiğinin kanıtı.
    /// `/kayit` formundaki "18 yaşından büyüğüm" onayı şu an yalnızca client
    /// tarafında (bkz. `web/app/(site)/kayit/page.tsx`) tutuluyor — bu alan, o
    /// onayın backend'de de kalıcı bir izini bırakabilmesi için ayrılmıştır.
    /// Doldurulması (kayıt akışının bu alana gerçekten yazması) ayrı bir görevdir.
    /// </summary>
    public DateTime? AgeConfirmedAt { get; set; }
}
