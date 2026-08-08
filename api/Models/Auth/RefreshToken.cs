namespace api.Models.Auth;

/// <summary>
/// docs/11-auth.md Bölüm 2.2 — DB'de yalnızca hash tutulur, ham değer hiçbir zaman
/// kalıcı kılınmaz (yalnızca HttpOnly cookie olarak client'a bir kez gönderilir).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid PlayerId { get; init; }

    /// <summary>Unique index — ham token'ın SHA-256 hash'i.</summary>
    public required string TokenHash { get; init; }

    public required DateTime ExpiresAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? RevokedAt { get; set; }
}
