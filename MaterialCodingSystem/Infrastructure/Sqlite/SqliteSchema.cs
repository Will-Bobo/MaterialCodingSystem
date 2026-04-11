using Dapper;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Infrastructure.Sqlite;

public static class SqliteSchema
{
    public static void EnsureCreated(SqliteConnection conn)
    {
        using var tx = conn.BeginTransaction();
        conn.Execute("""
                     CREATE TABLE IF NOT EXISTS category (
                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                       code TEXT NOT NULL UNIQUE,
                       name TEXT NOT NULL UNIQUE,
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
                     
                     CREATE INDEX IF NOT EXISTS idx_material_item_code ON material_item(code);
                     CREATE INDEX IF NOT EXISTS idx_material_item_spec ON material_item(spec);
                     CREATE INDEX IF NOT EXISTS idx_material_item_spec_normalized ON material_item(spec_normalized);
                     CREATE INDEX IF NOT EXISTS idx_item_category_spec_norm ON material_item(category_code, spec_normalized);
                     CREATE INDEX IF NOT EXISTS idx_material_item_group_id ON material_item(group_id);
                     CREATE INDEX IF NOT EXISTS idx_material_item_status ON material_item(status);
                     """,
            transaction: tx);
        tx.Commit();
    }
}

