namespace api;

/// <summary>
/// docs/21-payment-sandbox-e2e.md Bölüm 5: ödeme sağlayıcısının üç çalışma modu.
/// 🔒 <see cref="Sandbox"/> ile <see cref="Live"/> arasındaki tek fark config
/// değerleridir (BtcPayBaseUrl/ApiKey/StoreId/WebhookSecret) — kodda bu ikisini
/// ayıran bir davranış dallanması yazılmaz, aksi hâlde production'a geçiş
/// "config-only" olmaktan çıkar. Kodun ayırdığı tek şey provider seçiminin
/// kendisidir: sahte mi, gerçek BTCPay mi.
/// </summary>
public enum PaymentProviderMode
{
    /// <summary>Ağa hiç çıkmaz (<see cref="Services.Payments.FakePaymentProvider"/>). Günlük geliştirmenin varsayılanı.</summary>
    Fake,

    /// <summary>Gerçek Greenfield entegrasyonu, regtest BTCPay'e bağlı (müşterinin istediği iyzico-sandbox karşılığı).</summary>
    Sandbox,

    /// <summary>Gerçek Greenfield entegrasyonu, mainnet store'a bağlı.</summary>
    Live
}

/// <summary>
/// Ödeme modülünün tüm sayısal/konfigüre edilebilir değerleri (docs/05-payment.md
/// Bölüm 2.4). GameConfig'in aksine bu değerler <c>appsettings.json</c>'daki
/// "Payment" bölümünden <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>
/// ile bağlanır — çünkü BTCPay bağlantı bilgileri ve webhook secret'ı gibi bazı
/// alanlar ortama (regtest/testnet/mainnet) göre değişir ve derleme zamanı sabiti
/// olamaz. Kodda magic number kullanılmaz; her değer bu sınıftan okunur.
/// </summary>
public class PaymentConfig
{
    public const string SectionName = "Payment";

    // 🔒 Müşteri kararı (Bölüm 1.1): komisyon %10. Giriş ücreti artık sabit değil,
    // Room'dan gelir (Standart'ta sabit $1, VIP'de kurucunun seçtiği değer) — bu
    // yüzden burada ayrı bir sabit EntryFeeUsd alanı yoktur (v9).
    public decimal CommissionRate { get; set; } = 0.10m;

    // 🛠️ Bölüm 1.9: minimum yatırma müşterinin örneğinden ($1.00 ≈ 0.022 LTC).
    // Minimum çekim müşteri tarafından verilmedi, tutarlılık için aynı değer
    // varsayıldı — ❓ müşteriye doğrulatılmalı.
    public decimal MinDepositUsd { get; set; } = 1.00m;
    public decimal MinWithdrawalUsd { get; set; } = 1.00m;

    // 🔔🛠️❓ docs/07-pages.md "❓ Müşteriden Doğrulanması Gereken Noktalar":
    // "VIP oda giriş ücretinin alt/üst sınırı var mı... belirtilmedi." Alt sınır
    // zaten 0'dır (RoomService negatif değeri reddeder); üst sınır hiç yoktu —
    // kurucu teorik olarak $1.000.000 gibi bir değer girip anlamsız/kazara bir
    // oda oluşturabiliyordu. Geçici, tutucu varsayım: Standart oda giriş ücretinin
    // (GameConfig.StandardRoomEntryFeeUsd = $1) 500 katı — gerçek "high-roller"
    // VIP kullanımını engellemeyecek kadar geniş, ama kazara/anlamsız girişleri
    // (ör. $10.000+) engelleyecek kadar dar. Bu değer ONAYLANANA KADAR GEÇİCİDİR,
    // müşteriye raporlanmalı.
    public decimal MaxVipEntryFeeUsd { get; set; } = 500.00m;

    // 🛠️ Bölüm 1.2 — stale-cache üç kademeli politika süreleri (öneri değerler).
    public int PriceCacheFreshSeconds { get; set; } = 30;
    public int PriceCacheStaleMaxSeconds { get; set; } = 300;
    public int PriceQuoteValiditySeconds { get; set; } = 900;
    public int PriceOracleTimeoutSeconds { get; set; } = 5;

    // 🛠️ Bölüm 1.2 — ödeme toleransı ve overpayment refund eşiği.
    public decimal PaymentToleranceRate { get; set; } = 0.01m;
    public decimal RefundOverpaymentThresholdUsd { get; set; } = 1.00m;

    // 🔒 Bölüm 1.4 — regtest/testnet için 1 confirmation yeterli.
    public int RequiredConfirmations { get; set; } = 1;

    // 🔒 Bölüm 8.1 — webhook imza header adı configurable, "sha256=" prefix'i
    // ise WebhookSignatureValidator içinde sabit kod olarak tutulur (Bölüm 2.4).
    public string WebhookSignatureHeader { get; set; } = "BTCPay-Sig";
    public int WebhookMaxAgeSeconds { get; set; } = 300;

    // 🛠️ Bölüm 2.6 — v7'de sadeleştirildi: network fee sorumluluğu havuzdan
    // düşülür (kazanana net gönderilen tutar zaten fee dahil hesaplanır). Bu alan
    // yalnızca dokümantasyon/denetim amaçlı sabit bir etiket taşır.
    public string NetworkFeeResponsibility { get; set; } = "DeductedFromPool";

    public int WebhookEventRetentionDays { get; set; } = 90;

    // 🛠️ BTCPay Greenfield API bağlantı bilgileri — doküman Bölüm 2.4'te ayrıca
    // listelenmemiş ama IPaymentProvider'ın gerçek implementasyonu için zorunludur.
    // 🔒 docs/21-payment-sandbox-e2e.md Bölüm 5/8: `Sandbox` → `Live` geçişinde
    // değişen alanlar TAM OLARAK bunlardır (Mode ile birlikte) — koda dokunulmaz.
    // Bu dosyadaki varsayılanlar yalnızca `Fake` mod içindir; `Sandbox`/`Live`'da
    // biri boşsa uygulama başlamaz (Program.cs fail-fast). Değerler asla commit
    // edilmez, user-secrets/ortam değişkeni ile verilir (06-coding-standards.md).
    // 🐞 docs/21-payment-sandbox-e2e.md Aşama 6 (Bölüm 8) bulgusu: bu alanların
    // varsayılanları önceden "https://btcpay.example.local" ve "dev-webhook-secret"
    // gibi BOŞ OLMAYAN yer tutuculardı (appsettings.json'da da öyleydi). Bölüm 5'in
    // fail-fast kuralı "boşsa başlama" dediği için, `Live` modunda WebhookSecret'ı
    // vermeyi unutan bir operatörde uygulama HATA VERMEDEN başlıyor ve webhook
    // imzalarını depoda herkese açık olan bu yer tutucuyla doğruluyordu — sahte bir
    // "InvoiceSettled" webhook'u imzalayıp bakiye üretmek mümkün olurdu (sandbox'ta
    // uygulamanın bu şekilde fiilen başladığı doğrulandı). Varsayılanlar artık boş:
    // yer tutucu bir değer sessizce üretime sızamaz, fail-fast gerçekten devreye girer.
    public string BtcPayBaseUrl { get; set; } = string.Empty;
    public string BtcPayApiKey { get; set; } = string.Empty;
    public string BtcPayStoreId { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;

    // 🛠️ docs/21-payment-sandbox-e2e.md Bölüm 5: ödeme sağlayıcısının çalışma
    // modu. Önceki `UseFakeProviderInDevelopment` boolean'ı üç durumlu bir dünyayı
    // ifade edemiyordu ve modu ASP.NET ortamına (Development/Production)
    // yapıştırdığı için sandbox'ı ortamdan bağımsız çalıştırmayı zorlaştırıyordu.
    // Tanımsızsa varsayılan `Fake`'tir (en güvenli varsayılan) — bkz. Program.cs'teki
    // provider seçimi ve `Live` için fail-fast doğrulaması.
    public PaymentProviderMode Mode { get; set; } = PaymentProviderMode.Fake;

    // 🛠️ PostgreSQL bağlantı dizesi — ayrı bir persistence katmanı (Bölüm "ayrı
    // katman"). Yerel geliştirme ortamında `wintowar` adlı bir veritabanına
    // bağlanır; gerçek ortamlarda bu değer appsettings.{Environment}.json veya
    // ortam değişkeni ile ezilir, koda hardcode edilmez (bkz. `06-coding-standards.md`
    // "Secrets"). Testler ayrı bir SQLite in-memory bağlantısı kullanır (bkz.
    // api.Tests/TestSupport/PaymentDbContextFactory.cs) — gerçek Postgres
    // gerektirmeden hızlı çalışır.
    public string ConnectionString { get; set; } = "Host=localhost;Port=5432;Database=wintowar;Username=postgres;Password=postgres";

    /// <summary>
    /// 🔒 docs/21-payment-sandbox-e2e.md Bölüm 5 fail-fast kuralı: gerçek BTCPay'e
    /// bağlanan modlarda (`Sandbox`/`Live`) eksik olan zorunlu alanların adlarını
    /// döner; liste boş değilse uygulama başlatılmaz (bkz. Program.cs). Sessiz
    /// fallback (eksik config'de `Fake`'e düşmek) kesinlikle yasaktır.
    ///
    /// Kural Sandbox ve Live için AYNIDIR — mod başına farklı bir doğrulama yazmak,
    /// "Sandbox→Live geçişi config-only" garantisini bozardı.
    /// </summary>
    public IReadOnlyList<string> GetMissingBtcPayFieldNames()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(BtcPayBaseUrl)) { missing.Add("Payment:BtcPayBaseUrl"); }
        if (string.IsNullOrWhiteSpace(BtcPayApiKey)) { missing.Add("Payment:BtcPayApiKey"); }
        if (string.IsNullOrWhiteSpace(BtcPayStoreId)) { missing.Add("Payment:BtcPayStoreId"); }
        if (string.IsNullOrWhiteSpace(WebhookSecret)) { missing.Add("Payment:WebhookSecret"); }
        return missing;
    }
}
