namespace api;

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

    // 🔒 Müşteri kararı (Bölüm 1.1): giriş ücreti 1 USD, komisyon %10.
    public decimal EntryFeeUsd { get; set; } = 1.00m;
    public decimal CommissionRate { get; set; } = 0.10m;

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

    // 🛠️ Bölüm 1.6 — eşleşme bulunamama zaman aşımı.
    public int MatchmakingTimeoutSeconds { get; set; } = 120;

    // 🔒 Bölüm 8.1 — webhook imza header adı configurable, "sha256=" prefix'i
    // ise WebhookSignatureValidator içinde sabit kod olarak tutulur (Bölüm 2.4).
    public string WebhookSignatureHeader { get; set; } = "BTCPay-Sig";
    public int WebhookMaxAgeSeconds { get; set; } = 300;

    // 🛠️ Bölüm 0.3 — retry+backoff+jitter parametreleri (payout).
    public int PayoutRetryCount { get; set; } = 5;
    public int PayoutRetryBaseDelaySeconds { get; set; } = 10;
    public int PayoutRetryJitterSeconds { get; set; } = 5;

    // 🛠️ Bölüm 0.3 — retry+backoff+jitter parametreleri (refund).
    public int RefundRetryCount { get; set; } = 5;
    public int RefundRetryBaseDelaySeconds { get; set; } = 10;
    public int RefundRetryJitterSeconds { get; set; } = 5;

    // 🛠️ Reconciliation job — tarama aralığı, distributed lock timeout'u, geriye
    // dönük tarama penceresi.
    public int ReconciliationIntervalSeconds { get; set; } = 60;
    public int ReconciliationLockTimeoutSeconds { get; set; } = 30;
    public int ReconciliationScanWindowMinutes { get; set; } = 180;

    // 🛠️ Bölüm 2.6 — v7'de sadeleştirildi: network fee sorumluluğu havuzdan
    // düşülür (kazanana net gönderilen tutar zaten fee dahil hesaplanır). Bu alan
    // yalnızca dokümantasyon/denetim amaçlı sabit bir etiket taşır.
    public string NetworkFeeResponsibility { get; set; } = "DeductedFromPool";

    public int WebhookEventRetentionDays { get; set; } = 90;

    // 🛠️ BTCPay Greenfield API bağlantı bilgileri — doküman Bölüm 2.4'te ayrıca
    // listelenmemiş ama IPaymentProvider'ın gerçek implementasyonu için zorunludur.
    // Regtest/testnet erişilemediği için şu an FakePaymentProvider kullanılıyor
    // (bkz. Providers/FakePaymentProvider.cs); bu alanlar gerçek entegrasyon için
    // hazır tutulur.
    public string BtcPayBaseUrl { get; set; } = "https://btcpay.example.local";
    public string BtcPayApiKey { get; set; } = string.Empty;
    public string BtcPayStoreId { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = "dev-webhook-secret";

    // 🛠️ SQLite dosya yolu — ayrı bir persistence katmanı (Bölüm "ayrı katman").
    public string ConnectionString { get; set; } = "Data Source=payments.db";
}
