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
                     
                     -- material_item 的 spec 唯一性口径迁移：
                     -- 历史版本为表级 UNIQUE(category_code, spec)（会导致 status=0 仍占用 spec）。
                     -- PRD V1.3 冻结口径：仅启用态(status=1)唯一 -> 部分唯一索引。
                     """,
            transaction: tx);

        // 1) 确保 material_item 基表存在（用于后续 PRAGMA 检测）
        conn.Execute("""
                     CREATE TABLE IF NOT EXISTS material_item (
                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                       group_id INTEGER NOT NULL,
                       category_id INTEGER NOT NULL,
                       category_code TEXT NOT NULL,
                       code TEXT NOT NULL UNIQUE,
                       suffix TEXT NOT NULL,
                       name TEXT NOT NULL,
                       display_name TEXT NULL,
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
                     """, transaction: tx);

        // 2) 如未完成迁移，则把表级 UNIQUE(category_code, spec) 迁移为部分唯一索引：
        //    CREATE UNIQUE INDEX ... WHERE status=1
        EnsureSpecUniqueActiveOnlyMigrated(conn, tx);

        // 2.1) V1.4.1：display_name 列（BOM 显示名）增量迁移（旧 DB 无该列时自动补齐）
        EnsureMaterialItemDisplayNameColumnMigrated(conn, tx);

        // 3) 其他表与索引
        conn.Execute("""
                     
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

                     -- V1.4：BOM 轻量归档索引表（不存明细）
                     CREATE TABLE IF NOT EXISTS bom_archive (
                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                       finished_code TEXT NOT NULL,
                       version TEXT NOT NULL,
                       file_path TEXT NOT NULL,
                       created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                       UNIQUE(finished_code, version)
                     );

                     CREATE INDEX IF NOT EXISTS idx_bom_archive_finished_created
                     ON bom_archive(finished_code, created_at DESC);
                     """,
            transaction: tx);
        tx.Commit();
    }

    private static void EnsureSpecUniqueActiveOnlyMigrated(SqliteConnection conn, SqliteTransaction tx)
    {
        // 已存在部分唯一索引，则视为迁移完成（幂等）
        var hasActiveIndex = conn.ExecuteScalar<long>(
            """
            SELECT COUNT(1)
            FROM sqlite_master
            WHERE type='index' AND name='ux_material_item_category_spec_active';
            """,
            transaction: tx);
        if (hasActiveIndex > 0) return;

        // 通过建表 SQL 判断是否仍存在表级 UNIQUE(category_code, spec)
        var createSql = conn.ExecuteScalar<string?>(
            "SELECT sql FROM sqlite_master WHERE type='table' AND name='material_item' LIMIT 1;",
            transaction: tx) ?? string.Empty;

        var hasLegacyUnique = createSql.IndexOf("UNIQUE(category_code, spec)", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!hasLegacyUnique)
        {
            // 无旧约束：直接创建部分唯一索引（即使未来 schema 直接变更，此处也能补齐索引）
            conn.Execute(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS ux_material_item_category_spec_active
                ON material_item(category_code, spec)
                WHERE status = 1;
                """,
                transaction: tx);
            return;
        }

        // 安全迁移：重建 material_item 表，移除表级 UNIQUE(category_code, spec)，改为部分唯一索引
        conn.Execute("PRAGMA foreign_keys=OFF;", transaction: tx);

        conn.Execute("ALTER TABLE material_item RENAME TO material_item_old;", transaction: tx);

        conn.Execute("""
                     CREATE TABLE material_item (
                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                       group_id INTEGER NOT NULL,
                       category_id INTEGER NOT NULL,
                       category_code TEXT NOT NULL,
                       code TEXT NOT NULL UNIQUE,
                       suffix TEXT NOT NULL,
                       name TEXT NOT NULL,
                       display_name TEXT NULL,
                       description TEXT NOT NULL,
                       spec TEXT NOT NULL,
                       spec_normalized TEXT NOT NULL,
                       brand TEXT,
                       status INTEGER NOT NULL DEFAULT 1,
                       is_structured INTEGER DEFAULT 0,
                       created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                       UNIQUE(group_id, suffix),
                       FOREIGN KEY(group_id) REFERENCES material_group(id),
                       FOREIGN KEY(category_id) REFERENCES category(id),
                       CHECK(status IN (0,1))
                     );
                     """, transaction: tx);

        conn.Execute("""
                     INSERT INTO material_item(
                       id, group_id, category_id, category_code,
                       code, suffix, name, display_name, description, spec, spec_normalized, brand,
                       status, is_structured, created_at
                     )
                     SELECT
                       id, group_id, category_id, category_code,
                       code, suffix, name, NULL, description, spec, spec_normalized, brand,
                       status, is_structured, created_at
                     FROM material_item_old;
                     """, transaction: tx);

        // 迁移后重建索引（与 EnsureCreated 其余部分一致；这里先建关键索引，后续外层 CREATE INDEX IF NOT EXISTS 仍幂等）
        conn.Execute("""
                     CREATE INDEX IF NOT EXISTS idx_material_item_code ON material_item(code);
                     CREATE INDEX IF NOT EXISTS idx_material_item_spec ON material_item(spec);
                     CREATE INDEX IF NOT EXISTS idx_material_item_spec_normalized ON material_item(spec_normalized);
                     CREATE INDEX IF NOT EXISTS idx_item_category_spec_norm ON material_item(category_code, spec_normalized);
                     CREATE INDEX IF NOT EXISTS idx_material_item_group_id ON material_item(group_id);
                     CREATE INDEX IF NOT EXISTS idx_material_item_status ON material_item(status);
                     """, transaction: tx);

        conn.Execute(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS ux_material_item_category_spec_active
            ON material_item(category_code, spec)
            WHERE status = 1;
            """,
            transaction: tx);

        conn.Execute("DROP TABLE material_item_old;", transaction: tx);
        conn.Execute("PRAGMA foreign_keys=ON;", transaction: tx);
    }

    private static void EnsureMaterialItemDisplayNameColumnMigrated(SqliteConnection conn, SqliteTransaction tx)
    {
        // 幂等：通过 PRAGMA table_info 判断列是否存在
        var has = conn.ExecuteScalar<long>(
            """
            SELECT COUNT(1)
            FROM pragma_table_info('material_item')
            WHERE name = 'display_name';
            """,
            transaction: tx);
        if (has > 0) return;

        try
        {
            conn.Execute("ALTER TABLE material_item ADD COLUMN display_name TEXT NULL;", transaction: tx);
        }
        catch (Exception ex)
        {
            // 启动升级失败需要可追踪（轻量：抛出让上层捕获/记录）
            throw new InvalidOperationException("failed to migrate material_item.display_name", ex);
        }
    }
}

