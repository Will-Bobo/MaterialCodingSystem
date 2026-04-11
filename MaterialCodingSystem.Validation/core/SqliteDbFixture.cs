using Dapper;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Validation.core;

// Phase4: real sqlite db per ExecutionPlan (single-connection, isolated)
public sealed class SqliteDbFixture : DbFixture
{
    public SqliteConnection Connection { get; }
    private readonly Dictionary<string, object?> _raw = new();
    private int _seedSeq = 0;

    public SqliteDbFixture()
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        Connection.Open();
        CreateSchema();
    }

    public override void Reset()
    {
        _raw.Clear();
        _seedSeq = 0;
        // recreate schema by dropping tables; simplest for deterministic tests
        Connection.Execute("""
                           DROP TABLE IF EXISTS material_item;
                           DROP TABLE IF EXISTS material_group;
                           DROP TABLE IF EXISTS category;
                           """);
        CreateSchema();
    }

    public override void Seed(Dictionary<string, object?>? data)
    {
        if (data is null) return;

        foreach (var (k, v) in data) _raw[k] = v;

        // category
        if (data.TryGetValue("category", out var cat) && cat is System.Collections.IEnumerable catList)
        {
            foreach (var row in catList)
            {
                var d = NormalizeMap(row);
                if (d is null) continue;
                var code = d.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
                var name = d.TryGetValue("name", out var n) ? n?.ToString() ?? code : code;
                Connection.Execute("INSERT INTO category(code,name) VALUES (@code,@name);", new { code, name });
            }
        }

        // material_group
        if (data.TryGetValue("material_group", out var mg) && mg is System.Collections.IEnumerable mgList)
        {
            foreach (var row in mgList)
            {
                var d = NormalizeMap(row);
                if (d is null) continue;
                var id = d.TryGetValue("id", out var idv) ? Convert.ToInt32(idv) : 0;
                var categoryCode = d.TryGetValue("category_code", out var cc) ? cc?.ToString() ?? "" : "";
                var serialNo = d.TryGetValue("serial_no", out var sn) ? Convert.ToInt32(sn) : 1;

                Connection.Execute("INSERT OR IGNORE INTO category(code,name) VALUES (@code,@name);",
                    new { code = categoryCode, name = categoryCode });

                if (id > 0)
                {
                    Connection.Execute("""
                                       INSERT INTO material_group(id, category_id, category_code, serial_no)
                                       VALUES (@id, (SELECT id FROM category WHERE code=@categoryCode), @categoryCode, @serialNo);
                                       """, new { id, categoryCode, serialNo });
                }
                else
                {
                    Connection.Execute("""
                                       INSERT INTO material_group(category_id, category_code, serial_no)
                                       VALUES ((SELECT id FROM category WHERE code=@categoryCode), @categoryCode, @serialNo);
                                       """, new { categoryCode, serialNo });
                }
            }
        }

        // material_item
        if (data.TryGetValue("material_item", out var mi) && mi is System.Collections.IEnumerable miList)
        {
            foreach (var row in miList)
            {
                var d = NormalizeMap(row);
                if (d is null) continue;
                var groupId = d.TryGetValue("group_id", out var gid) ? Convert.ToInt32(gid) : 1;
                var suffix = (d.TryGetValue("suffix", out var s) ? s?.ToString() : "A") ?? "A";
                var categoryCode = d.TryGetValue("category_code", out var cc) ? cc?.ToString() : null;
                var spec = d.TryGetValue("spec", out var sp) ? sp?.ToString() ?? "" : "";
                var description = d.TryGetValue("description", out var dd) ? dd?.ToString() ?? "" : "";
                var specNorm = d.TryGetValue("spec_normalized", out var sn) ? sn?.ToString() ?? "" : "";
                var name = d.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
                var brand = d.TryGetValue("brand", out var b) ? b?.ToString() : null;
                var status = d.TryGetValue("status", out var st) ? Convert.ToInt32(st) : 1;

                if (string.IsNullOrWhiteSpace(spec))
                {
                    // Some spec cases seed items only for suffix topology; ensure unique spec to avoid UNIQUE(category_code,spec) collisions
                    _seedSeq++;
                    spec = $"__SEED__{groupId}_{suffix}_{_seedSeq}";
                }

                if (string.IsNullOrWhiteSpace(categoryCode))
                {
                    categoryCode = Connection.ExecuteScalar<string?>(
                        "SELECT category_code FROM material_group WHERE id=@groupId;",
                        new { groupId }
                    );
                }

                if (string.IsNullOrWhiteSpace(categoryCode))
                {
                    // if neither category_code nor group exists, use a default placeholder to allow uniqueness tests
                    categoryCode = "ZDA";
                }

                Connection.Execute("INSERT OR IGNORE INTO category(code,name) VALUES (@code,@name);",
                    new { code = categoryCode, name = categoryCode });

                // Ensure material_group exists for this group_id (some cases seed material_item without group)
                var groupExists = Connection.ExecuteScalar<long?>(
                    "SELECT 1 FROM material_group WHERE id=@groupId LIMIT 1;",
                    new { groupId }
                ) is not null;
                if (!groupExists)
                {
                    Connection.Execute("""
                                       INSERT OR IGNORE INTO material_group(id, category_id, category_code, serial_no)
                                       VALUES (@groupId, (SELECT id FROM category WHERE code=@categoryCode), @categoryCode, 1);
                                       """,
                        new { groupId, categoryCode });
                }

                var code = d.TryGetValue("code", out var cv) ? cv?.ToString() : null;
                if (string.IsNullOrWhiteSpace(code))
                {
                    var serial = Connection.ExecuteScalar<long>(
                        "SELECT serial_no FROM material_group WHERE id=@groupId;",
                        new { groupId }
                    );
                    code = MaterialCodingSystem.Domain.Services.CodeGenerator.GenerateItemCode(categoryCode!, (int)serial, suffix[0]);
                }

                if (string.IsNullOrWhiteSpace(specNorm))
                {
                    specNorm = MaterialCodingSystem.Domain.Services.SpecNormalizer.NormalizeV1(description);
                }

                Connection.Execute("""
                                   INSERT INTO material_item(
                                     group_id, category_id, category_code, code, suffix,
                                     name, description, spec, spec_normalized, brand, status, is_structured
                                   )
                                   VALUES(
                                     @groupId,
                                     (SELECT id FROM category WHERE code=@categoryCode),
                                     @categoryCode, @code, @suffix,
                                     @name, @description, @spec, @specNorm, @brand, @status, 0
                                   );
                                   """,
                    new
                    {
                        groupId,
                        categoryCode,
                        code,
                        suffix,
                        name,
                        description,
                        spec,
                        specNorm,
                        brand,
                        status
                    });
            }
        }
    }

    public override object? Get(string key) => _raw.TryGetValue(key, out var v) ? v : null;

    public bool Exists(string table, Dictionary<string, object?> where)
    {
        var cols = where.Keys.ToList();
        var conditions = string.Join(" AND ", cols.Select(c => $"{c}=@{c}"));
        var sql = $"SELECT 1 FROM {table} WHERE {conditions} LIMIT 1;";
        var found = Connection.ExecuteScalar<int?>(sql, where);
        return found is not null;
    }

    private void CreateSchema()
    {
        Connection.Execute("""
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
                           """);
    }

    private static Dictionary<string, object?>? NormalizeMap(object? v)
    {
        if (v is Dictionary<string, object?> dso) return dso;

        if (v is IDictionary<object, object> d)
        {
            var r = new Dictionary<string, object?>();
            foreach (var (k, vv) in d) r[k.ToString() ?? ""] = vv;
            return r;
        }

        return null;
    }
}

