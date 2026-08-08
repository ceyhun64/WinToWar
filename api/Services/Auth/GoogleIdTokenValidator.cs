using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace api.Services.Auth;

/// <summary>
/// docs/11-auth.md Bölüm 1.2 🛠️ kütüphane istisnası: <c>Google.Apis.Auth</c> NuGet
/// paketi eklenir — id_token doğrulaması Google'ın public key rotasyonuna karşı
/// elle güvenli şekilde yeniden yazılabilecek bir iş değildir, resmi kütüphane
/// kullanmamak güvenlik riski oluşturur (06-coding-standards.md Bağımlılık
/// Disiplini'nin öngördüğü meşru istisna).
/// </summary>
public class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly AuthConfig _config;
    private readonly ILogger<GoogleIdTokenValidator> _logger;

    public GoogleIdTokenValidator(IOptions<AuthConfig> config, ILogger<GoogleIdTokenValidator> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_config.GoogleClientId],
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return new GoogleIdentity(payload.Subject, payload.Email, payload.Name ?? payload.Email);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogInformation("Geçersiz Google id_token reddedildi: {Reason}", ex.Message);
            return null;
        }
    }
}
