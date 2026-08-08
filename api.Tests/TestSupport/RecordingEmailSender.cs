using api.Services.Auth;

namespace api.Tests.TestSupport;

/// <summary>
/// Testlerde gerçek bir e-posta sağlayıcısı olmadan reset/verify token'ının
/// (e-posta gövdesine gömülü, bkz. AuthService) doğrulanabilmesi için kaydeden
/// bir sahte implementasyon (01-workflow-rules.md Bölüm 0.4 istisnası: test dosyaları hariç).
/// </summary>
public class RecordingEmailSender : IEmailSender
{
    public List<(string ToEmail, string Subject, string BodyText)> SentEmails { get; } = new();

    public Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken cancellationToken)
    {
        SentEmails.Add((toEmail, subject, bodyText));
        return Task.CompletedTask;
    }
}
