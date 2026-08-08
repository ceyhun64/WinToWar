using api.Models.Auth;
using Microsoft.EntityFrameworkCore;

namespace api.Services.Auth;

/// <summary>
/// docs/11-auth.md Bölüm 0.0 kritik mimari tespiti: projede kalıcı bir kimlik
/// deposu yoktu (yalnızca PaymentDbContext/GameEventDbContext vardı, ikisi de
/// kendi modülüne ait). Bu, o modüllerden tamamen ayrı, üçüncü bir persistence
/// katmanıdır (bkz. 01-workflow-rules.md Bölüm 0.13 modüller arası izolasyon) —
/// PostgreSQL üzerinde çalışır, aynı tek-instance Postgres'i paylaşır ama tabloları
/// tamamen ayrıdır. Testler PaymentDbContext ile aynı desende ayrı bir SQLite
/// in-memory bağlantısı kullanır (bkz. 06-coding-standards.md test istisnası).
/// </summary>
public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<PlayerAccount> PlayerAccounts => Set<PlayerAccount>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<AccountDeletionRequest> AccountDeletionRequests => Set<AccountDeletionRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.GoogleId).IsUnique();
            entity.Property(e => e.Role).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.PlayerId);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
        });

        modelBuilder.Entity<EmailVerificationToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
        });

        modelBuilder.Entity<AccountDeletionRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>();
        });
    }
}
