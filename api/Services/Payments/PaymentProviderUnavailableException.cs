namespace api.Services.Payments;

/// <summary>
/// Ödeme sağlayıcısına (BTCPay) <b>hiç ulaşılamadığında</b> fırlatılır: TCP bağlantısı
/// kurulamadı, DNS çözülemedi, TLS el sıkışması başarısız oldu ya da istek zaman aşımına
/// uğradı. Yani sağlayıcı bir cevap ÜRETMEDİ.
///
/// Sağlayıcının ürettiği bir hata yanıtı (4xx/5xx) bu tipe girmez — o, iş kuralı ya da
/// yapılandırma hatasıdır ve kasıtlı olarak ayrı kalır; bu istisna yalnızca "servis şu an
/// erişilemiyor, istek hiç işlenmedi" durumunu temsil eder.
///
/// <see cref="PriceOracleUnavailableException"/> ile aynı desendedir: erişilemeyen bir dış
/// bağımlılık, istemciye tipli bir <b>503</b> olarak yansır. Bu tip olmadan aynı durum
/// yakalanmamış bir <c>HttpRequestException</c> olarak 500'e düşüyordu — Development'ta
/// tarayıcıya tam stack trace sızdıran, kullanıcıya hiçbir şey anlatmayan bir yanıt.
/// </summary>
public class PaymentProviderUnavailableException : Exception
{
    public PaymentProviderUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
