namespace api.Models.Auth;

/// <summary>
/// docs/11-auth.md Bölüm 2.2 — yalnızca parola-akışı kayıtlarında üretilir
/// (Google akışında Google zaten e-postayı doğrulamış olduğundan gerek yoktur, Bölüm 1.2).
/// </summary>
public class EmailVerificationToken
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid PlayerId { get; init; }

    /// <summary>Unique index — ham token'ın SHA-256 hash'i.</summary>
    public required string TokenHash { get; init; }

    public required DateTime ExpiresAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UsedAt { get; set; }
}
