using Dapper;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Infrastructure.Sqlite;

public static class SqliteSchema
{
    private const int CurrentSchemaVersion = 3;

    public static void EnsureCreated(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();
        // 0) app_meta + schema_version
        conn.Execute("""
                     CREATE TABLE IF NOT EXISTS app_meta (
                       k TEXT PRIMARY KEY,
                       v TEXT NOT NULL
                     );
                     """, transaction: tx);

        var hasVersion = conn.ExecuteScalar<long>(
            "SELECT COUNT(1) FROM app_meta WHERE k='schema_version';",
            transaction: tx) > 0;
        if (!hasVersion)
        {
            // legacy db: assume v1 baseline before any explicit migrations
            conn.Execute("INSERT INTO app_meta(k,v) VALUES ('schema_version','1');", transaction: tx);
        }

        var versionStr = conn.ExecuteScalar<string>(
            "SELECT v FROM app_meta WHERE k='schema_version' LIMIT 1;",
            transaction: tx);
        if (!int.TryParse(versionStr, out var version) || version <= 0)
            throw new InvalidOperationException("Invalid schema_version in app_meta.");

        if (version > CurrentSchemaVersion)
            throw new InvalidOperationException($"Database schema_version={version} is newer than app version={CurrentSchemaVersion}.");

        // 1) Fresh db bootstrap: if no category table, create latest schema and set version
        var hasCategoryTable = conn.ExecuteScalar<long>(
            "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='category';",
            transaction: tx) > 0;
        if (!hasCategoryTable)
        {
            CreateLatestSchema(conn, tx);
            conn.Execute("UPDATE app_meta SET v=@v WHERE k='schema_version';",
                new { v = CurrentSchemaVersion.ToString() }, transaction: tx);
            tx.Commit();
            return;
        }

        // 2) Migrations (idempotent, sequential)
        if (version < 2)
        {
            MigrateToV2(conn, tx);
            version = 2;
            conn.Execute("UPDATE app_meta SET v='2' WHERE k='schema_version';", transaction: tx);
        }

        if (version < 3)
        {
            MigrateToV3(conn, tx);
            version = 3;
            conn.Execute("UPDATE app_meta SET v='3' WHERE k='schema_version';", transaction: tx);
        }

        // 3) Post-check (detect half-upgrade / drift)
        ValidateSchema(conn, tx, version);

        tx.Commit();
    }

    private static void CreateLatestSchema(SqliteConnection conn, SqliteTransaction tx)
    {
        conn.Execute("""
                     CREATE TABLE IF NOT EXISTS category (
                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                       code TEXT NOT NULL UNIQUE,
                       name TEXT NOT NULL UNIQUE,
                       start_serial_no INTEGER NOT NULL DEFAULT 1,
                       created_at TEXT DEFAULT CURRENT_TIMESTAMP
                     );
                     
                     CREATE TABLE IF NOT EXISTS material_group (
                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                       category_id INTEGER NOT NULL,
                       category_code TEXT NOT NULL,
                       serial_no INTEGER NOT NULL,
                       created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                       UNIQUE(category_id, serial_no),
                       FOREIGN KEY(category_id) REFERENCES category(id)
                     );
                     
                     CREATE TABLE IF NOT EXISTS material_item (
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
                     
                     CREATE TABLE IF NOT EXISTS material_attribute (
                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                       material_item_id INTEGER NOT NULL,
                       attr_key TEXT NOT NULL,
                       attr_value TEXT NOT NULL,
                       created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                       FOREIGN KEY(material_item_id) REFERENCES material_item(id)
                     );

                     CREATE TABLE IF NOT EXISTS create_request_log (
                       request_id TEXT PRIMARY KEY,
                       op TEXT NOT NULL,
                       group_id INTEGER NOT NULL,
                       category_code TEXT NOT NULL,
                       serial_no INTEGER NOT NULL,
                       code TEXT NOT NULL,
                       suffix TEXT NOT NULL,
                       spec TEXT NOT NULL,
                       spec_normalized TEXT NOT NULL,
                       created_at TEXT DEFAULT CURRENT_TIMESTAMP
                     );
                     
                     CREATE INDEX IF NOT EXISTS idx_material_item_code ON material_item(code);
                     CREATE INDEX IF NOT EXISTS idx_material_item_spec ON material_item(spec);
                     CREATE INDEX IF NOT EXISTS idx_material_item_spec_normalized ON material_item(spec_normalized);
                     CREATE INDEX IF NOT EXISTS idx_item_category_spec_norm ON material_item(category_code, spec_normalized);
                     CREATE INDEX IF NOT EXISTS idx_material_item_group_id ON material_item(group_id);
                     CREATE INDEX IF NOT EXISTS idx_material_item_status ON material_item(status);
                     """,
            transaction: tx);
    }

    // 1 -> 2: add category.start_serial_no
    private static void MigrateToV2(SqliteConnection conn, SqliteTransaction tx)
    {
        var hasStartSerialNo = conn.ExecuteScalar<long>(
            "SELECT COUNT(1) FROM pragma_table_info('category') WHERE name='start_serial_no';",
            transaction: tx) > 0;
        if (!hasStartSerialNo)
        {
            conn.Execute("ALTER TABLE category ADD COLUMN start_serial_no INTEGER NOT NULL DEFAULT 1;",
                transaction: tx);
        }

        // idempotent data fix (defensive): ensure no NULL
        conn.Execute("UPDATE category SET start_serial_no=1 WHERE start_serial_no IS NULL;",
            transaction: tx);
    }

    // 2 -> 3: create request log table for idempotency
    private static void MigrateToV3(SqliteConnection conn, SqliteTransaction tx)
    {
        conn.Execute("""
                     CREATE TABLE IF NOT EXISTS create_request_log (
                       request_id TEXT PRIMARY KEY,
                       op TEXT NOT NULL,
                       group_id INTEGER NOT NULL,
                       category_code TEXT NOT NULL,
                       serial_no INTEGER NOT NULL,
                       code TEXT NOT NULL,
                       suffix TEXT NOT NULL,
                       spec TEXT NOT NULL,
                       spec_normalized TEXT NOT NULL,
                       created_at TEXT DEFAULT CURRENT_TIMESTAMP
                     );
                     """, transaction: tx);
    }

    private static void ValidateSchema(SqliteConnection conn, SqliteTransaction tx, int version)
    {
        if (version >= 2)
        {
            var hasStartSerialNo = conn.ExecuteScalar<long>(
                "SELECT COUNT(1) FROM pragma_table_info('category') WHERE name='start_serial_no';",
                transaction: tx) > 0;
            if (!hasStartSerialNo)
                throw new InvalidOperationException("schema_version>=2 but category.start_serial_no column is missing.");
        }

        if (version >= 3)
        {
            var hasLog = conn.ExecuteScalar<long>(
                "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='create_request_log';",
                transaction: tx) > 0;
            if (!hasLog)
                throw new InvalidOperationException("schema_version>=3 but create_request_log table is missing.");
        }
    }
}

