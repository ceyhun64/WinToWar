namespace api.Models.Auth;

public enum PlayerRole
{
    Player,
    Admin
}

public enum PlayerStatus
{
    Active,
    Suspended,
    PendingDeletion,
    Deleted
}

/// <summary>
/// docs/11-auth.md Bölüm 0.0 — kritik mimari tespit: mevcut <see cref="api.Models.Player"/>
/// bir hesap değil, tek bir maçın ömrüyle sınırlı, bellek içi bir maç-katılımcısı
/// nesnesidir (Slot/ConnectionId/IsEliminated/ActionWindow gibi tamamen çalışma-zamanı
/// alanları taşır, MatchManager tarafından her maç katılımında yeniden yaratılır).
/// Bölüm 1.3'ün "mevcut Player.cs genişletilir" kararı bu gerçek yapıyla çelişiyor:
/// Email/PasswordHash/Role gibi kalıcı kimlik alanlarını o sınıfa eklemek, iki
/// bağımsız sorumluluğu (kalıcı hesap kimliği vs. tek maçlık oyun durumu) tek bir
/// EF Core entity'sinde birleştirir — bu hem 01-workflow-rules.md Bölüm 0.13
/// (modüller arası izolasyon/SRP) hem de 02-architecture.md'nin "Model yalnızca
/// veri taşır, iş mantığı Service katmanındadır" ilkesiyle çelişir, ayrıca
/// ConnectionId/Slot gibi alanların EF Core'a persist edilmesi anlamsızdır.
/// Bölüm 0.0'ın kendi kapanış hükmü ("kanıtlanmamış varsayımla yazılmıştır, gerçek
/// durum farklıysa gerçek durum esas alınır") gereği: mevcut Player.cs'e
/// DOKUNULMAZ (tamamen ayrı, oyun motoruna ait kalır), yerine bu yeni, ayrı
/// <see cref="PlayerAccount"/> entity'si eklenir. "Player" terminolojisi
/// (Wallet.PlayerId, PaymentInvoice.PlayerId ile tutarlılık — Bölüm 1.3'ün asıl
/// koruduğu şey) isimde korunur; <see cref="Id"/> bu id'lerle aynı string temsille
/// (Guid.ToString()) eşleşir, ödeme modülünün hiçbir dosyasına dokunulmaz (Bölüm 0.4).
/// </summary>
public class PlayerAccount
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Email { get; set; }

    /// <summary>Google-only hesapta null (Bölüm 1.5). Guard: PasswordHash ve GoogleId aynı anda null olamaz.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Bölüm 1.2 — unique index (nullable).</summary>
    public string? GoogleId { get; set; }

    public required string DisplayName { get; set; }

    public PlayerRole Role { get; set; } = PlayerRole.Player;

    public PlayerStatus Status { get; set; } = PlayerStatus.Active;

    public DateTime? EmailVerifiedAt { get; set; }

    public DateTime? AgeConfirmedAt { get; set; }

    public DateTime? TermsAcceptedAt { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
}
