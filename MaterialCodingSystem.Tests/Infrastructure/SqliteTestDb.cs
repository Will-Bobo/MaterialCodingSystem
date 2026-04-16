using Dapper;
using MaterialCodingSystem.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Tests.Infrastructure;

internal sealed class SqliteTestDb : IAsyncDisposable
{
    public SqliteConnection Connection { get; }
    public string ConnectionString { get; }

    private SqliteTestDb(SqliteConnection connection, string connectionString)
    {
        Connection = connection;
        ConnectionString = connectionString;
    }

    public static async Task<SqliteTestDb> CreateAsync()
    {
        var cs = "Data Source=:memory:";
        var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        // Use real schema migrator to keep tests aligned with app behavior.
        SqliteSchema.EnsureCreated(conn);

        return new SqliteTestDb(conn, cs);
    }

    // 共享内存库：保持 master connection 打开，其他连接用同一 connection string 加入
    public static async Task<SqliteTestDb> CreateSharedAsync(string name)
    {
        var cs = $"Data Source=file:{name}?mode=memory&cache=shared";
        var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        SqliteSchema.EnsureCreated(conn);

        return new SqliteTestDb(conn, cs);
    }

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync();
    }
}

