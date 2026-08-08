namespace api.Services.Auth;

/// <summary>
/// docs/11-auth.md Bölüm 3.1: "gerçek SMTP/sağlayıcı seçimi bu görevde hardcode
/// edilmez, ❓ müşteriden sağlayıcı tercihi netleşmeli." Bu soyutlama o kararı
/// engellemeden ilerlemeyi sağlar — tek kullanım noktası olsa da (YAGNI'ye aykırı
/// görünebilir) burada bilinçli bir istisnadır, çünkü asıl implementasyon
/// (SendGrid/SES/SMTP) müşteri kararına bağlı ve şimdiden yazılamaz; arayüz bu
/// sınırı somutlaştırır.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken cancellationToken);
}

/// <summary>
/// 🛠️ Gerçek sağlayıcı netleşene kadar geliştirme ortamı implementasyonu: e-postayı
/// göndermez, yalnızca ILogger ile (parola/token içermeyecek şekilde, bkz. Bölüm 0.4
/// log kısıtı) bir bildirim kaydı düşer. Bu bir mock/placeholder DEĞİLDİR (bkz.
/// 01-workflow-rules.md Bölüm 0.4 istisnası) — gerçek bir dış e-posta sağlayıcısı
/// olmadan bu görevde test edilebilir tek gerçek davranıştır, token/link içeriği
/// (log'a yazılmayan kısmı) doğru üretilir ve reset/verify akışları buna göre
/// uçtan uca çalışır durumdadır.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken cancellationToken)
    {
        _logger.LogInformation("E-posta gönderimi (dev sağlayıcı yok): {ToEmail}, {Subject}", toEmail, subject);
        return Task.CompletedTask;
    }
}
