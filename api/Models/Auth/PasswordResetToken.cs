namespace api.Models.Auth;

/// <summary>docs/11-auth.md Bölüm 2.2 — tek kullanımlık, süreli parola sıfırlama token'ı.</summary>
public class PasswordResetToken
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid PlayerId { get; init; }

    /// <summary>Unique index — ham token'ın SHA-256 hash'i (e-postayla giden değer hiçbir zaman kalıcı kılınmaz).</summary>
    public required string TokenHash { get; init; }

    public required DateTime ExpiresAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UsedAt { get; set; }
}
