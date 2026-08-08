namespace api;

/// <summary>
/// Kimlik doğrulama modülünün tüm sayısal/konfigüre edilebilir değerleri
/// (docs/11-auth.md Bölüm 2.4). PaymentConfig ile aynı desen: appsettings.json'daki
/// "Auth" bölümünden <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>
/// ile bağlanır — JWT imzalama anahtarı ve Google client id gibi alanlar ortama
/// göre değişir ve derleme zamanı sabiti olamaz (bkz. 06-coding-standards.md
/// "Secrets"). Kodda magic number kullanılmaz; her değer bu sınıftan okunur.
/// </summary>
public class AuthConfig
{
    public const string SectionName = "Auth";

    // 🛠️ Bölüm 1.4 — kısa ömürlü access token + rotating refresh token.
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
    public int RefreshTokenLifetimeDays { get; set; } = 30;

    public int PasswordResetTokenExpirySeconds { get; set; } = 900;
    public int EmailVerificationTokenExpirySeconds { get; set; } = 86400;

    // 🛠️ Bölüm 1.6 — yalnızca parola akışı.
    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int LockoutDurationMinutes { get; set; } = 15;
    public int LoginRateLimitPerMinute { get; set; } = 10;
    public int RegisterRateLimitPerHour { get; set; } = 5;
    public int ForgotPasswordRateLimitPerHour { get; set; } = 5;

    // 🛠️ Bölüm 1.4 — çalıntı refresh token tekrar kullanılırsa tüm aktif token'lar iptal edilir.
    public bool RevokeAllOnReuseDetected { get; set; } = true;

    public int MinPasswordLength { get; set; } = 8;

    // 🛠️ JWT imzalama anahtarı — ortam değişkeninden/appsettings'ten okunur, hardcode edilmez.
    // Gerçek ortamda .env/secrets manager ile ezilir (bkz. appsettings.json'daki dev-only değer).
    public string JwtSigningKey { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = "WinToWar";
    public string JwtAudience { get; set; } = "WinToWar";

    // Bölüm 1.2 — Google Identity Services id_token doğrulaması için beklenen audience.
    public string GoogleClientId { get; set; } = string.Empty;

    // Bölüm 1.9 — ilk admin self-servis kayıttan oluşturulamaz, yalnızca env var'dan seed edilir.
    public string SeedAdminEmail { get; set; } = string.Empty;
    public string SeedAdminPassword { get; set; } = string.Empty;

    // 🛠️ Ayrı bir persistence katmanı (Bölüm 0.0 — yeni AuthDbContext kararı).
    // Diğer modüllerle aynı tek-instance Postgres'i paylaşır (bkz. PaymentConfig.ConnectionString
    // yorumu), ayrı bir tablo kümesidir.
    public string ConnectionString { get; set; } = "Host=localhost;Port=5432;Database=wintowar;Username=postgres;Password=postgres";
}
