namespace api.Services.Payments;

/// <summary>
/// "BtcPay" adlı <see cref="HttpClient"/>'a takılan taşıma katmanı koruması: sağlayıcıya
/// ulaşılamadığında ortaya çıkan <see cref="HttpRequestException"/> /
/// <see cref="TaskCanceledException"/>'ı tipli bir
/// <see cref="PaymentProviderUnavailableException"/>'a çevirir.
///
/// 🛠️ Neden <see cref="BtcPayGreenfieldProvider"/>'ın içinde try/catch değil de handler:
/// provider BTCPay'e beş ayrı uçtan gidiyor (invoice oluşturma, payment-methods, on-chain
/// gönderim, transaction sorgusu). Her birini tek tek sarmalamak aynı kodu beş kez
/// tekrarlamak ve ileride eklenecek altıncı çağrıda unutmak demekti. Handler, çağrıların
/// TAMAMINI tek noktadan kapsar ve provider'ın iş mantığına hiç dokunmaz.
///
/// ⚠️ Kapsamı bilinçli olarak dar: yalnızca "cevap hiç alınamadı" durumunu çevirir.
/// <c>EnsureSuccessStatusCode()</c>'un bir 4xx/5xx yanıt için fırlattığı
/// <see cref="HttpRequestException"/> provider'ın içinde, yani bu handler'dan SONRA
/// oluşur — dolayısıyla buraya hiç uğramaz ve eskisi gibi davranmaya devam eder.
/// Sağlayıcının ürettiği hata yanıtları "erişilemiyor" ile aynı kefeye konmaz.
/// </summary>
public class PaymentProviderTransportHandler : DelegatingHandler
{
    private readonly ILogger<PaymentProviderTransportHandler> _logger;

    public PaymentProviderTransportHandler(ILogger<PaymentProviderTransportHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ödeme sağlayıcısına bağlanılamadı: {Method} {Uri}", request.Method, request.RequestUri);
            throw new PaymentProviderUnavailableException(
                "Ödeme sağlayıcısına şu anda ulaşılamıyor. Lütfen birazdan tekrar deneyin.", ex);
        }
        // İstemci isteği iptal ettiyse bu bir kullanılamama durumu değildir (ör. kullanıcı
        // sayfadan ayrıldı) — o zaman iptal olduğu gibi yukarı geçer.
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Ödeme sağlayıcısı zaman aşımına uğradı: {Method} {Uri}", request.Method, request.RequestUri);
            throw new PaymentProviderUnavailableException(
                "Ödeme sağlayıcısı zamanında yanıt vermedi. Lütfen birazdan tekrar deneyin.", ex);
        }
    }
}
