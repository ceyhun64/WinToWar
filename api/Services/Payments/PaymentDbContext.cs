using api.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace api.Services.Payments;

/// <summary>
/// Ödeme modülünün ana oyun motorundan tamamen ayrı persistence katmanı
/// (bkz. docs/05-payment.md giriş bölümü). PostgreSQL üzerinde çalışır (Npgsql) —
/// gerçek transaction, unique constraint ve ACID garantileri gerektiği için
/// (Bölüm 0.3, 8.3) bellek içi bir çözüm yerine gerçek bir ilişkisel veritabanı
/// seçildi. `api.Tests/` içindeki testler ayrı bir SQLite in-memory bağlantısı
/// kullanır (bkz. 06-coding-standards.md test altyapısı istisnası notu) — bu,
/// yalnızca hızlı izole testler içindir, gerçek çalışma zamanı her zaman
/// PostgreSQL'dir. Şema `Database.Migrate()` (Program.cs) ile, `Migrations/Payments/`
/// altındaki EF Core migration'ları üzerinden yönetilir.
/// </summary>
public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<PaymentInvoice> PaymentInvoices => Set<PaymentInvoice>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<PayoutRecipient> PayoutRecipients => Set<PayoutRecipient>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WithdrawalRequest> WithdrawalRequests => Set<WithdrawalRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentInvoice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.BtcPayInvoiceId).IsUnique();
            entity.Property(e => e.AmountUsd).HasPrecision(18, 2);
            entity.Property(e => e.AmountLtc).HasPrecision(18, 8);
            entity.Property(e => e.LockedUsdPerLtc).HasPrecision(18, 8);
            entity.Property(e => e.PriceOracleSource).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.MatchJoinOutcome).HasConversion<string>();
            entity.Ignore(e => e.StatusRank);
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(e => e.PlayerId);
            entity.Property(e => e.BalanceUsd).HasPrecision(18, 2);
            entity.Property(e => e.KycStatus).HasConversion<string>().HasDefaultValue(KycStatus.NotRequired);
        });

        modelBuilder.Entity<WithdrawalRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AmountUsd).HasPrecision(18, 2);
            entity.Property(e => e.AmountLtc).HasPrecision(18, 8);
            entity.Property(e => e.Status).HasConversion<string>();
        });

        modelBuilder.Entity<Payout>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MatchId).IsUnique();
            entity.Property(e => e.TotalPoolUsd).HasPrecision(18, 2);
            entity.Property(e => e.CommissionUsd).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PayoutRecipient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.PayoutId, e.WinnerPlayerId }).IsUnique();
            entity.Property(e => e.AmountUsd).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Refund>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PaymentInvoiceId).IsUnique();
            entity.Property(e => e.AmountUsd).HasPrecision(18, 2);
            entity.Property(e => e.Reason).HasConversion<string>();
        });

        modelBuilder.Entity<ProcessedWebhookEvent>(entity =>
        {
            entity.HasKey(e => e.EventId);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardWalletBalanceNonNegative();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardWalletBalanceNonNegative();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Negatif bakiye asla oluşmaz (bkz. 02-architecture.md "Uç Durumlar") — son savunma hattı burada.</summary>
    private void GuardWalletBalanceNonNegative()
    {
        foreach (var entry in ChangeTracker.Entries<Wallet>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified && entry.Entity.BalanceUsd < 0)
            {
                throw new InvalidOperationException($"Wallet bakiyesi negatif olamaz (PlayerId={entry.Entity.PlayerId}).");
            }
        }
    }
}
