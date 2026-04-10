using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MaterialCodingSystem.Validation.infrastructure;

/// <summary>
/// 每次 Create() 使用独立 :memory: 连接，互不共享数据。
/// </summary>
public sealed class TestDbContextProvider : IDbContextProvider
{
    public AppDbContext Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
