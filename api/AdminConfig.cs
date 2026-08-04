namespace api;

/// <summary>
/// 🛠️ docs/07-pages.md ❓: "Admin rolünün oyuncu hesaplarından nasıl ayrıldığı
/// müşteri belirtmedi" — projede henüz bir kullanıcı/rol tablosu olmadığından
/// (bkz. `web/lib/identity.ts`), en basit çözüm olarak paylaşılan bir erişim
/// anahtarı kullanılır. Gerçek bir Role tabanlı auth eklendiğinde bu, ilgili
/// admin controller'larındaki `[AdminAuth]` filtresiyle birlikte değiştirilir.
/// </summary>
public class AdminConfig
{
    public const string SectionName = "Admin";
    public string AccessKey { get; set; } = "dev-admin-key";

    /// <summary>docs/07-pages.md `/admin/loglar`: bellekteki halka tamponun tutacağı en fazla kayıt sayısı.</summary>
    public int MaxLogEntries { get; set; } = 500;
}
