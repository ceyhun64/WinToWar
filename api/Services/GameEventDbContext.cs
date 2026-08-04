using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

/// <summary>
/// docs/02-architecture.md "Maç Denetim Kaydı": oyun motorunun tek kalıcı deposu —
/// maç state'inin kendisi bilerek bellek içi kalır (MatchManager), yalnızca destek/
/// itiraz kanıtı olarak MatchEventLog burada kalıcılaşır. Ödeme modülünün
/// PaymentDbContext'inden bilinçli olarak ayrı tutulur (bkz. `01-workflow-rules.md`
/// Bölüm 0.13 modüller arası izolasyon — oyun motoru, Payments-namespaced bir
/// DbContext'e bağımlı olmaz); aynı fiziksel Postgres instance'ını (tek-instance
/// varsayımı, bkz. `02-architecture.md` "Ölçeklenebilirlik") paylaşması yalnızca
/// bir altyapı/connection-string detayıdır, veri modeli tamamen ayrıdır.
/// </summary>
public class GameEventDbContext : DbContext
{
    public GameEventDbContext(DbContextOptions<GameEventDbContext> options) : base(options)
    {
    }

    public DbSet<MatchEventLog> MatchEventLogs => Set<MatchEventLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MatchEventLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MatchId, e.SequenceNo }).IsUnique();
            entity.Property(e => e.EventType).HasConversion<string>();
        });
    }
}
