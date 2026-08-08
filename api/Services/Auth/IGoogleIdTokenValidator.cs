namespace api.Services.Auth;

public record GoogleIdentity(string GoogleId, string Email, string DisplayName);

/// <summary>
/// docs/11-auth.md Bölüm 1.2: id_token backend'de Google'ın public key'lerine karşı
/// imza + aud/iss/exp doğrulanır — frontend'in ilettiği email/isim asla doğrudan
/// güvenilmez.
/// </summary>
public interface IGoogleIdTokenValidator
{
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken);
}
