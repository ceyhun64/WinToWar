using api;

namespace api.Tests;

/// <summary>
/// 🐞 Regresyon — docs/21-payment-sandbox-e2e.md Bölüm 5 (fail-fast) ve Bölüm 8
/// (production'a geçişin config-only olduğunun kanıtı).
///
/// Sandbox'ta bulunan gerçek açık: `BtcPayBaseUrl`/`WebhookSecret` alanlarının
/// varsayılanları BOŞ OLMAYAN yer tutuculardı ("https://btcpay.example.local",
/// "dev-webhook-secret"). Fail-fast kuralı "boşsa başlama" dediği için, `Live`
/// modunda `WebhookSecret` vermeyi unutan bir operatörde uygulama sorunsuz
/// başlıyordu ve webhook imzaları depoda herkese açık olan yer tutucuyla
/// doğrulanıyordu — sahte bir "InvoiceSettled" webhook'u imzalayıp bakiye üretmek
/// mümkün olurdu. Bu testler yer tutucuların geri gelmesini engeller.
/// </summary>
public class PaymentConfigFailFastTests
{
    [Fact]
    public void DefaultConfig_HasNoPlaceholderBtcPaySecrets_SoFailFastActuallyTriggers()
    {
        var config = new PaymentConfig();

        var missing = config.GetMissingBtcPayFieldNames();

        Assert.Equal(
            new[]
            {
                "Payment:BtcPayBaseUrl",
                "Payment:BtcPayApiKey",
                "Payment:BtcPayStoreId",
                "Payment:WebhookSecret"
            },
            missing);
    }

    [Fact]
    public void FullyConfigured_ReportsNothingMissing()
    {
        var config = new PaymentConfig
        {
            BtcPayBaseUrl = "http://btcpayserver:49392",
            BtcPayApiKey = "key",
            BtcPayStoreId = "store",
            WebhookSecret = "secret"
        };

        Assert.Empty(config.GetMissingBtcPayFieldNames());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankWebhookSecret_IsReportedMissing(string webhookSecret)
    {
        // Yalnızca "" değil, yalnızca boşluk içeren bir değer de eksik sayılmalı —
        // aksi hâlde gerçek imza doğrulaması anlamsız bir anahtarla çalışırdı.
        var config = new PaymentConfig
        {
            BtcPayBaseUrl = "http://btcpayserver:49392",
            BtcPayApiKey = "key",
            BtcPayStoreId = "store",
            WebhookSecret = webhookSecret
        };

        Assert.Equal(new[] { "Payment:WebhookSecret" }, config.GetMissingBtcPayFieldNames());
    }

    /// <summary>
    /// 🔒 Bölüm 5: `Fake` en güvenli varsayılandır — config hiç verilmemişse ağa
    /// çıkan bir mod asla kendiliğinden seçilmez.
    /// </summary>
    [Fact]
    public void DefaultMode_IsFake()
    {
        Assert.Equal(PaymentProviderMode.Fake, new PaymentConfig().Mode);
    }
}
