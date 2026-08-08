using api.Services.Auth;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace api.Tests.TestSupport;

/// <summary>PaymentDbContextFactory ile aynı desen — bkz. o dosyanın gerekçesi.</summary>
public static class AuthDbContextFactory
{
    public static (AuthDbContext Db, SqliteConnection Connection) CreateOpen()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AuthDbContext(options);
        db.Database.EnsureCreated();
        return (db, connection);
    }
}
