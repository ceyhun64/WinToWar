using api.Services.Payments;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace api.Tests.TestSupport;

/// <summary>
/// Testler için gerçek bir SQLite veritabanı (in-memory, bağlantı açık tutularak)
/// kurar — unique constraint / transaction / DbUpdateException davranışlarının
/// (Bölüm 8.3) gerçek bir ilişkisel veritabanına karşı doğrulanabilmesi için
/// InMemory provider yerine bilinçli olarak gerçek SQLite kullanılır.
/// </summary>
public static class PaymentDbContextFactory
{
    public static (PaymentDbContext Db, SqliteConnection Connection) CreateOpen()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new PaymentDbContext(options);
        db.Database.EnsureCreated();
        return (db, connection);
    }
}
