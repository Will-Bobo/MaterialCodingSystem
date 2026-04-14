using Dapper;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Application.Interfaces;
using MaterialCodingSystem.Domain.Entities;
using MaterialCodingSystem.Domain.ValueObjects;
using Microsoft.Data.Sqlite;

namespace MaterialCodingSystem.Infrastructure.Sqlite;

public sealed class SqliteMaterialRepository : IMaterialRepository
{
    private readonly SqliteConnection _connection;

    /// <summary>
    /// MUST be used within <see cref="SqliteUnitOfWork"/> for all write operations (INSERT/UPDATE/DELETE).
    /// </summary>
    public SqliteMaterialRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    private SqliteTransaction? Tx => AmbientSqliteContext.CurrentTransaction;

    private static void EnsureWriteTransaction()
    {
        if (AmbientSqliteContext.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Write operation must be executed within a transaction.");
        }
    }

    /// <summary>
    /// 将 SQLite UNIQUE 违反（错误码 19）按消息中的表名+列组合映射为仓储约束标识；顺序与评审一致。
    /// </summary>
    internal static string MapSqliteUniqueConstraintViolation(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return IMaterialRepository.CONSTRAINT_UNKNOWN;

        var m = message;

        bool HasCi(string sub) => m.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0;

        // 1) material_item: category_code + spec（避免把 spec_normalized 列误判为 spec）
        if (HasCi("material_item.category_code") && HasItemSpecColumn(m))
            return IMaterialRepository.CONSTRAINT_ITEM_CATEGORY_SPEC;

        // 2) material_item: group_id + suffix
        if (HasCi("material_item.group_id") && HasCi("material_item.suffix"))
            return IMaterialRepository.CONSTRAINT_ITEM_GROUP_SUFFIX;

        // 3) material_item: code
        if (HasCi("material_item.code"))
            return IMaterialRepository.CONSTRAINT_ITEM_CODE;

        // 4) material_group: category_id + serial_no
        if (HasCi("material_group.category_id") && HasCi("material_group.serial_no"))
            return IMaterialRepository.CONSTRAINT_GROUP_CATEGORY_SERIAL;

        return IMaterialRepository.CONSTRAINT_UNKNOWN;
    }

    /// <summary>消息中出现 material_item 的 spec 列（非 spec_normalized）。</summary>
    private static bool HasItemSpecColumn(string m)
    {
        const string needle = "material_item.spec";
        var idx = 0;
        while (true)
        {
            idx = m.IndexOf(needle, idx, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;
            var after = idx + needle.Length;
            if (after >= m.Length || m[after] != '_')
                return true;
            idx = after;
        }
    }

    public async Task<bool> CategoryExistsAsync(CategoryCode categoryCode, CancellationToken ct = default)
    {
        var sql = "SELECT 1 FROM category WHERE code = @code LIMIT 1;";
        var found = await _connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            sql, new { code = categoryCode.Value }, transaction: Tx, cancellationToken: ct));
        return found is not null;
    }

    public async Task<string?> GetCategoryNameByCodeAsync(CategoryCode categoryCode, CancellationToken ct = default)
    {
        var sql = "SELECT name FROM category WHERE code = @code LIMIT 1;";
        return await _connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            sql, new { code = categoryCode.Value }, transaction: Tx, cancellationToken: ct));
    }

    public async Task InsertCategoryAsync(string code, string name, CancellationToken ct = default)
    {
        EnsureWriteTransaction();
        var sql = "INSERT INTO category(code, name) VALUES (@code, @name);";
        try
        {
            await _connection.ExecuteAsync(new CommandDefinition(
                sql, new { code, name }, transaction: Tx, cancellationToken: ct));
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            // SQLite 的错误信息里通常会包含字段名；Application 侧再做一次细分映射
            throw new DbConstraintViolationException("UNIQUE(category)", ex.Message);
        }
    }

    public async Task<IReadOnlyList<(string Code, string Name)>> ListCategoriesAsync(CancellationToken ct = default)
    {
        var sql = "SELECT code AS Code, name AS Name FROM category ORDER BY code;";
        var rows = await _connection.QueryAsync<(string Code, string Name)>(new CommandDefinition(
            sql, transaction: Tx, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<bool> SpecExistsAsync(CategoryCode categoryCode, Spec spec, CancellationToken ct = default)
    {
        var sql = "SELECT 1 FROM material_item WHERE category_code = @categoryCode AND spec = @spec LIMIT 1;";
        var found = await _connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            sql, new { categoryCode = categoryCode.Value, spec = spec.Value }, transaction: Tx, cancellationToken: ct));
        return found is not null;
    }

    public async Task<int> GetMaxSerialNoAsync(CategoryCode categoryCode, CancellationToken ct = default)
    {
        var sql = @"
SELECT MAX(serial_no)
FROM material_group
WHERE category_code = @categoryCode;";
        return await _connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            sql, new { categoryCode = categoryCode.Value }, transaction: Tx, cancellationToken: ct)) ?? 0;
    }

    public async Task<int> InsertGroupAsync(CategoryCode categoryCode, int serialNo, CancellationToken ct = default)
    {
        EnsureWriteTransaction();
        var sql = @"
INSERT INTO material_group(category_id, category_code, serial_no)
VALUES ((SELECT id FROM category WHERE code=@categoryCode), @categoryCode, @serialNo);
SELECT last_insert_rowid();";

        try
        {
            var id = await _connection.ExecuteScalarAsync<long>(new CommandDefinition(
                sql, new { categoryCode = categoryCode.Value, serialNo }, transaction: Tx, cancellationToken: ct));
            return (int)id;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // constraint violation
        {
            var mapped = MapSqliteUniqueConstraintViolation(ex.Message);
            throw new DbConstraintViolationException(mapped, ex.Message);
        }
    }

    public async Task InsertItemAsync(int groupId, MaterialItem item, CancellationToken ct = default)
    {
        EnsureWriteTransaction();
        var sql = @"
INSERT INTO material_item(
  group_id, category_id, category_code,
  code, suffix, name, description, spec, spec_normalized, brand,
  status, is_structured
)
SELECT
  @groupId,
  mg.category_id,
  mg.category_code,
  @code, @suffix, @name, @description, @spec, @specNormalized, @brand,
  1, 0
FROM material_group mg
WHERE mg.id = @groupId;
";

        try
        {
            var rows = await _connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    groupId,
                    code = item.Code,
                    suffix = item.Suffix.Value.ToString(),
                    name = item.Name,
                    description = item.Description,
                    spec = item.Spec.Value,
                    specNormalized = item.SpecNormalized.Value,
                    brand = item.Brand
                },
                transaction: Tx,
                cancellationToken: ct
            ));

            if (rows != 1)
            {
                throw new DbConstraintViolationException("NOT_FOUND", "group not found for insert item.");
            }
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            var mapped = MapSqliteUniqueConstraintViolation(ex.Message);
            throw new DbConstraintViolationException(mapped, ex.Message);
        }
    }

    public async Task<MaterialGroupSnapshot?> GetGroupSnapshotAsync(int groupId, CancellationToken ct = default)
    {
        var groupSql = @"
SELECT mg.id AS GroupId, mg.category_code AS CategoryCode, mg.serial_no AS SerialNo
FROM material_group mg
WHERE mg.id = @groupId;";

        var group = await _connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(
            groupSql, new { groupId }, transaction: Tx, cancellationToken: ct));

        if (group is null) return null;

        var suffixSql = "SELECT suffix FROM material_item WHERE group_id = @groupId ORDER BY suffix;";
        var suffixStrings = (await _connection.QueryAsync<string>(new CommandDefinition(
            suffixSql, new { groupId }, transaction: Tx, cancellationToken: ct))).ToArray();

        var suffixes = suffixStrings
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s[0])
            .ToArray();

        return new MaterialGroupSnapshot(
            GroupId: (int)group.GroupId,
            CategoryCode: new CategoryCode((string)group.CategoryCode),
            SerialNo: (int)group.SerialNo,
            ExistingSuffixes: suffixes
        );
    }

    public async Task<MaterialItemStatusSnapshot?> GetBaseItemStatusByGroupIdAsync(int groupId, CancellationToken ct = default)
    {
        var sql = "SELECT code AS Code, status AS Status FROM material_item WHERE group_id=@groupId AND suffix='A' LIMIT 1;";
        var row = await _connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(
            sql, new { groupId }, transaction: Tx, cancellationToken: ct));
        if (row is null) return null;
        return new MaterialItemStatusSnapshot((string)row.Code, (int)row.Status);
    }

    public async Task<MaterialItemStatusSnapshot?> GetItemStatusByCodeAsync(string code, CancellationToken ct = default)
    {
        var sql = "SELECT code AS Code, status AS Status FROM material_item WHERE code=@code LIMIT 1;";
        var row = await _connection.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(
            sql, new { code }, transaction: Tx, cancellationToken: ct));
        if (row is null) return null;
        return new MaterialItemStatusSnapshot((string)row.Code, (int)row.Status);
    }

    public async Task<int?> GetGroupIdByItemCodeAsync(string code, CancellationToken ct = default)
    {
        var sql = "SELECT group_id FROM material_item WHERE code=@code LIMIT 1;";
        return await _connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            sql, new { code }, transaction: Tx, cancellationToken: ct));
    }

    public async Task DeprecateByCodeAsync(string code, CancellationToken ct = default)
    {
        EnsureWriteTransaction();
        var sql = "UPDATE material_item SET status=0 WHERE code=@code;";
        var rows = await _connection.ExecuteAsync(new CommandDefinition(
            sql, new { code }, transaction: Tx, cancellationToken: ct));
        if (rows != 1)
        {
            throw new DbConstraintViolationException("NOT_FOUND", "item not found for deprecate.");
        }
    }

    public async Task<PagedResult<MaterialItemSummary>> SearchByCodeAsync(SearchQuery query, CancellationToken ct = default)
    {
        var limit = query.Limit;
        var offset = query.Offset;
        var includeDeprecated = query.IncludeDeprecated;

        var prefixPattern = (query.CodeKeyword ?? "") + "%";
        var fuzzyPattern = "%" + (query.CodeKeyword ?? "") + "%";

        var statusFilter = includeDeprecated ? "" : "AND status = 1";
        var categoryFilter = string.IsNullOrWhiteSpace(query.CategoryCode) ? "" : "AND category_code = @categoryCode";

        async Task<List<MaterialItemSummary>> QueryAsync(string pattern)
        {
            var sql = $@"
SELECT code AS Code, name AS Name, spec AS Spec, description AS Description, brand AS Brand, status AS Status
FROM material_item
WHERE code LIKE @pattern
  {statusFilter}
  {categoryFilter}
ORDER BY code
LIMIT @limit OFFSET @offset;";

            return (await _connection.QueryAsync<MaterialItemSummary>(new CommandDefinition(
                sql,
                new { pattern, limit, offset, categoryCode = query.CategoryCode },
                transaction: Tx,
                cancellationToken: ct
            ))).ToList();
        }

        var first = await QueryAsync(prefixPattern);
        if (first.Count >= limit)
        {
            return new PagedResult<MaterialItemSummary>(Total: first.Count, Items: first);
        }

        var second = await QueryAsync(fuzzyPattern);
        var merged = first.Concat(second).GroupBy(x => x.Code).Select(g => g.First()).Take(limit).ToList();
        return new PagedResult<MaterialItemSummary>(Total: merged.Count, Items: merged);
    }

    public async Task<PagedResult<MaterialItemSpecHit>> SearchBySpecAsync(SearchQuery query, CancellationToken ct = default)
    {
        var sql = @"
SELECT code AS Code, spec AS Spec, description AS Description, name AS Name, brand AS Brand, status AS Status, group_id AS GroupId
FROM material_item
WHERE category_code = @categoryCode
  AND status = 1
  AND (
        spec LIKE '%' || @keyword || '%'
     OR spec_normalized LIKE '%' || @keyword || '%'
  )
LIMIT 20;";

        var items = (await _connection.QueryAsync<MaterialItemSpecHit>(new CommandDefinition(
            sql,
            new { categoryCode = query.CategoryCode, keyword = query.SpecKeyword },
            transaction: Tx,
            cancellationToken: ct
        ))).ToList();

        return new PagedResult<MaterialItemSpecHit>(Total: items.Count, Items: items);
    }

    public async Task<PagedResult<MaterialItemSpecHit>> SearchCandidatesBySpecOnlyAsync(
        string categoryCode,
        string keyword,
        int limit,
        CancellationToken ct = default)
    {
        // 候选收敛：仅 spec LIKE；固定 status=1；排序可复现（用于回归）。
        var sql = @"
SELECT
  mi.code AS Code,
  mi.spec AS Spec,
  mi.description AS Description,
  mi.name AS Name,
  mi.brand AS Brand,
  mi.status AS Status,
  mi.group_id AS GroupId
FROM material_item mi
JOIN material_group mg ON mi.group_id = mg.id
WHERE mi.category_code = @categoryCode
  AND mi.status = 1
  AND mi.spec LIKE '%' || @keyword || '%'
ORDER BY
  CASE
    WHEN LOWER(TRIM(mi.spec)) = LOWER(TRIM(@keyword)) THEN 0
    WHEN mi.spec LIKE '%' || @keyword || '%' THEN 1
    ELSE 2
  END,
  mg.serial_no,
  mi.suffix,
  mi.code
LIMIT @limit;";

        var items = (await _connection.QueryAsync<MaterialItemSpecHit>(new CommandDefinition(
            sql,
            new { categoryCode, keyword, limit },
            transaction: Tx,
            cancellationToken: ct
        ))).ToList();

        return new PagedResult<MaterialItemSpecHit>(Total: items.Count, Items: items);
    }

    public async Task<PagedResult<MaterialItemSpecHit>> SearchBySpecAllAsync(string keyword, bool includeDeprecated, int limit, CancellationToken ct = default)
    {
        var statusFilter = includeDeprecated ? "" : "AND mi.status = 1";
        var sql = $@"
SELECT
  mi.code AS Code,
  mi.spec AS Spec,
  mi.description AS Description,
  mi.name AS Name,
  mi.brand AS Brand,
  mi.status AS Status,
  mi.group_id AS GroupId
FROM material_item mi
JOIN material_group mg ON mi.group_id = mg.id
WHERE (
       mi.spec LIKE '%' || @keyword || '%'
    OR mi.spec_normalized LIKE '%' || @keyword || '%'
  )
  {statusFilter}
ORDER BY
  CASE
    WHEN mi.spec LIKE '%' || @keyword || '%' THEN 0
    WHEN mi.spec_normalized LIKE '%' || @keyword || '%' THEN 1
    ELSE 2
  END,
  mi.category_code,
  mg.serial_no,
  mi.suffix,
  mi.code
LIMIT @limit;";

        var items = (await _connection.QueryAsync<MaterialItemSpecHit>(new CommandDefinition(
            sql,
            new { keyword, limit },
            transaction: Tx,
            cancellationToken: ct
        ))).ToList();

        return new PagedResult<MaterialItemSpecHit>(Total: items.Count, Items: items);
    }

    public async Task<IReadOnlyList<MaterialExportRow>> ListActiveItemsForExportAsync(CancellationToken ct = default)
    {
        var sql = @"
SELECT
  mi.code AS Code,
  mi.spec AS Spec,
  mi.description AS Description,
  mi.brand AS Brand,
  mg.category_code AS CategoryCode,
  mg.serial_no AS SerialNo,
  mi.suffix AS Suffix,
  mi.status AS Status,
  c.name AS Name
FROM material_item mi
JOIN material_group mg ON mi.group_id = mg.id
JOIN category c ON mg.category_id = c.id
WHERE mi.status = 1
ORDER BY mi.status DESC, mg.category_code, mg.serial_no, mi.suffix, mi.code;";

        var list = (await _connection.QueryAsync<MaterialExportRow>(new CommandDefinition(
            sql, transaction: Tx, cancellationToken: ct))).ToList();
        return list;
    }

    public async Task<IReadOnlyList<MaterialExportRow>> ListAllItemsForExportAsync(CancellationToken ct = default)
    {
        var sql = @"
SELECT
  mi.code AS Code,
  mi.spec AS Spec,
  mi.description AS Description,
  mi.brand AS Brand,
  mg.category_code AS CategoryCode,
  mg.serial_no AS SerialNo,
  mi.suffix AS Suffix,
  mi.status AS Status,
  c.name AS Name
FROM material_item mi
JOIN material_group mg ON mi.group_id = mg.id
JOIN category c ON mg.category_id = c.id
ORDER BY mi.status DESC, mg.category_code, mg.serial_no, mi.suffix, mi.code;";

        var list = (await _connection.QueryAsync<MaterialExportRow>(new CommandDefinition(
            sql, transaction: Tx, cancellationToken: ct))).ToList();
        return list;
    }
}

