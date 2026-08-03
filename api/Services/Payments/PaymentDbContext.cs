using api.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace api.Services.Payments;

/// <summary>
/// Ödeme modülünün ana oyun motorundan tamamen ayrı persistence katmanı
/// (bkz. docs/05-payment.md giriş bölümü). SQLite üzerinde çalışır — gerçek
/// transaction, unique constraint ve ACID garantileri gerektiği için (Bölüm 0.3,
/// 8.3) bellek içi bir çözüm yerine gerçek bir ilişkisel veritabanı seçildi.
/// 🛠️ Migration tooling kurulmadı; şema <c>Database.EnsureCreated()</c> ile
/// (Program.cs) oluşturulur — ileride gerçek migration'lara geçilebilir.
/// </summary>
public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<PaymentInvoice> PaymentInvoices => Set<PaymentInvoice>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();
    public DbSet<ReconciliationLock> ReconciliationLocks => Set<ReconciliationLock>();

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
            entity.Property(e => e.PayoutAddressFormat).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Ignore(e => e.StatusRank);
        });

        modelBuilder.Entity<Payout>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MatchId).IsUnique();
            entity.Property(e => e.TotalPoolLtc).HasPrecision(18, 8);
            entity.Property(e => e.CommissionLtc).HasPrecision(18, 8);
            entity.Property(e => e.NetworkFeeLtc).HasPrecision(18, 8);
            entity.Property(e => e.AmountLtc).HasPrecision(18, 8);
            entity.Property(e => e.Status).HasConversion<string>();
        });

        modelBuilder.Entity<Refund>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PaymentInvoiceId).IsUnique();
            entity.Property(e => e.AmountLtc).HasPrecision(18, 8);
            entity.Property(e => e.Reason).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
        });

        modelBuilder.Entity<ProcessedWebhookEvent>(entity =>
        {
            entity.HasKey(e => e.EventId);
        });

        modelBuilder.Entity<ReconciliationLock>(entity =>
        {
            entity.HasKey(e => e.LockName);
        });
    }

    /// <summary>
    /// Bölüm 1.5: PayoutAddress immutable — update endpoint yok, ayrıca burada
    /// EF Core SaveChanges guard'ı ile veritabanı seviyesinde de garanti altına
    /// alınır. Bir PaymentInvoice.PayoutAddress'i değiştirmeye çalışan herhangi
    /// bir SaveChanges çağrısı reddedilir.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardPayoutAddressImmutability();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardPayoutAddressImmutability();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void GuardPayoutAddressImmutability()
    {
        foreach (var entry in ChangeTracker.Entries<PaymentInvoice>())
        {
            if (entry.State == EntityState.Modified &&
                entry.Property(e => e.PayoutAddress).IsModified)
            {
                throw new InvalidOperationException(
                    $"PaymentInvoice.PayoutAddress immutable'dır, değiştirilemez (InvoiceId={entry.Entity.Id}).");
            }
        }
    }
}
