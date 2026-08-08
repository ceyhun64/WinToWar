using api.Services.Auth;

namespace api.Tests.TestSupport;

/// <summary>
/// docs/11-auth.md Bölüm 1.2: gerçek Google.Apis.Auth doğrulaması dış bir ağ
/// çağrısı gerektirir — testlerde her çağrının döneceği kimliği (veya "geçersiz
/// token" durumunu) doğrudan kontrol eden sahte bir implementasyon kullanılır
/// (01-workflow-rules.md Bölüm 0.4 istisnası: test dosyaları hariç).
/// </summary>
public class FakeGoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly Dictionary<string, GoogleIdentity> _tokens = new();

    public void Register(string idToken, GoogleIdentity identity) => _tokens[idToken] = identity;

    public Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken)
        => Task.FromResult(_tokens.TryGetValue(idToken, out var identity) ? identity : null);
}
