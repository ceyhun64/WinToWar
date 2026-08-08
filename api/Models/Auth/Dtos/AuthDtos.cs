namespace api.Models.Auth.Dtos;

/// <summary>
/// docs/11-auth.md Bölüm 6/2.5: REST üzerinden client'a giden DTO'lar.
/// web/lib/auth/types.ts içindeki TypeScript tipleri bunlarla birebir eşleşir.
/// </summary>
public class AuthErrorResponse
{
    public required string Code { get; init; }
    public required string Message { get; init; }
}

public class RegisterRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string DisplayName { get; init; }
    public required bool AgeConfirmed { get; init; }
    public required bool TermsAccepted { get; init; }
}

public class LoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}

/// <summary>
/// docs/11-auth.md Bölüm 1.2: yeni bir Google hesabı ilk kez oluşturulacaksa
/// AgeConfirmed/TermsAccepted de doldurulmalıdır (Bölüm 1.8) — zaten var olan bir
/// hesapla giriş yapılırken bu iki alan yok sayılır.
/// </summary>
public class GoogleAuthRequest
{
    public required string IdToken { get; init; }
    public bool AgeConfirmed { get; init; }
    public bool TermsAccepted { get; init; }
}

public class GoogleLinkRequest
{
    public required string IdToken { get; init; }
}

public class ForgotPasswordRequest
{
    public required string Email { get; init; }
}

public class ResetPasswordRequest
{
    public required string Token { get; init; }
    public required string NewPassword { get; init; }
}

public class ChangePasswordRequest
{
    /// <summary>Google-only hesapta (henüz hiç parola belirlenmemiş) null/boş olabilir.</summary>
    public string? CurrentPassword { get; init; }
    public required string NewPassword { get; init; }
}

public class VerifyEmailRequest
{
    public required string Token { get; init; }
}

public class PlayerAccountDto
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required string Role { get; init; }
    public required string Status { get; init; }
    public required bool EmailVerified { get; init; }
    public required bool HasPassword { get; init; }
    public required bool GoogleLinked { get; init; }
}

public class AuthResponseDto
{
    public required string AccessToken { get; init; }
    public required DateTime AccessTokenExpiresAtUtc { get; init; }
    public required PlayerAccountDto Player { get; init; }
}
