using Dapper;
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

        // PRD V1 DDL（最小可测集合）
        await conn.ExecuteAsync(@"
CREATE TABLE category (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  code TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL UNIQUE,
  created_at TEXT DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE material_group (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  category_id INTEGER NOT NULL,
  category_code TEXT NOT NULL,
  serial_no INTEGER NOT NULL,
  created_at TEXT DEFAULT CURRENT_TIMESTAMP,
  UNIQUE(category_id, serial_no),
  FOREIGN KEY(category_id) REFERENCES category(id)
);

CREATE TABLE material_item (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  group_id INTEGER NOT NULL,
  category_id INTEGER NOT NULL,
  category_code TEXT NOT NULL,
  code TEXT NOT NULL UNIQUE,
  suffix TEXT NOT NULL,
  name TEXT NOT NULL,
  description TEXT NOT NULL,
  spec TEXT NOT NULL,
  spec_normalized TEXT NOT NULL,
  brand TEXT,
  status INTEGER NOT NULL DEFAULT 1,
  is_structured INTEGER DEFAULT 0,
  created_at TEXT DEFAULT CURRENT_TIMESTAMP,
  UNIQUE(group_id, suffix),
  UNIQUE(category_code, spec),
  FOREIGN KEY(group_id) REFERENCES material_group(id),
  FOREIGN KEY(category_id) REFERENCES category(id),
  CHECK(status IN (0,1))
);
");

        return new SqliteTestDb(conn, cs);
    }

    // 共享内存库：保持 master connection 打开，其他连接用同一 connection string 加入
    public static async Task<SqliteTestDb> CreateSharedAsync(string name)
    {
        var cs = $"Data Source=file:{name}?mode=memory&cache=shared";
        var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        await conn.ExecuteAsync(@"
CREATE TABLE category (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  code TEXT NOT NULL UNIQUE,
  name TEXT NOT NULL UNIQUE,
  created_at TEXT DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE material_group (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  category_id INTEGER NOT NULL,
  category_code TEXT NOT NULL,
  serial_no INTEGER NOT NULL,
  created_at TEXT DEFAULT CURRENT_TIMESTAMP,
  UNIQUE(category_id, serial_no),
  FOREIGN KEY(category_id) REFERENCES category(id)
);

CREATE TABLE material_item (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  group_id INTEGER NOT NULL,
  category_id INTEGER NOT NULL,
  category_code TEXT NOT NULL,
  code TEXT NOT NULL UNIQUE,
  suffix TEXT NOT NULL,
  name TEXT NOT NULL,
  description TEXT NOT NULL,
  spec TEXT NOT NULL,
  spec_normalized TEXT NOT NULL,
  brand TEXT,
  status INTEGER NOT NULL DEFAULT 1,
  is_structured INTEGER DEFAULT 0,
  created_at TEXT DEFAULT CURRENT_TIMESTAMP,
  UNIQUE(group_id, suffix),
  UNIQUE(category_code, spec),
  FOREIGN KEY(group_id) REFERENCES material_group(id),
  FOREIGN KEY(category_id) REFERENCES category(id),
  CHECK(status IN (0,1))
);
");

        return new SqliteTestDb(conn, cs);
    }

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync();
    }
}

