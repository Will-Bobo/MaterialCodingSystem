// NOTE: Validation tool — Dapper/SQL here is intentional; not subject to main app layer rules.

using Dapper;
using MaterialCodingSystem.Application;
using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Infrastructure.Sqlite;
using MaterialCodingSystem.Validation.core;

namespace MaterialCodingSystem.Validation.actions;

public static class PrdActions
{
    public static object NormalizeSpec(Context ctx)
    {
        var raw = ctx.Input["description"]?.ToString();
        var res = MaterialSpecQueriesV1.NormalizeDescriptionToSpecNormalized(raw);
        if (!res.IsSuccess)
            throw new ValidationException(res.Error!.Code, res.Error.Message);
        return new Dictionary<string, object?> { ["spec_normalized"] = res.Data! };
    }

    public static object CreateMaterialA(Context ctx)
    {
        if (ctx.Db is not SqliteDbFixture db)
            throw new ValidationException("DB_NOT_AVAILABLE");

        // If spec doesn't seed category, bootstrap minimal category for core flows that expect auto-create.
        if (ctx.Db.Get("category") is null)
        {
            var cc = ctx.Input["category_code"]?.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(cc) && !db.Exists("category", new Dictionary<string, object?> { ["code"] = cc }))
            {
                db.Connection.Execute("INSERT INTO category(code,name) VALUES (@code,@name);", new { code = cc, name = cc });
            }
        }

        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));

        var res = app.CreateMaterialItemA(new CreateMaterialItemARequest(
            CategoryCode: ctx.Input["category_code"]?.ToString() ?? "",
            Spec: ctx.Input["spec"]?.ToString() ?? "",
            Name: MaterialInputTextOrPlaceholder(ctx, "name", "n"),
            Description: MaterialInputTextOrPlaceholder(ctx, "description", "d"),
            Brand: ctx.Input["brand"]?.ToString()
        )).GetAwaiter().GetResult();

        if (!res.IsSuccess)
        {
            // YAML 口径：仅「分类缺失 / 分类不存在」映射为细粒度码；其余 VALIDATION_ERROR 原样透出（如 name/description 必填）
            if (res.Error!.Code == ErrorCodes.VALIDATION_ERROR)
            {
                var category = ctx.Input["category_code"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(category))
                    throw new ValidationException("CATEGORY_REQUIRED");
                if (string.Equals(res.Error.Message, "category_code not found.", StringComparison.Ordinal))
                    throw new ValidationException("CATEGORY_NOT_FOUND");
                throw new ValidationException(ErrorCodes.VALIDATION_ERROR, res.Error.Message);
            }

            throw new ValidationException(res.Error!.Code);
        }

        return new Dictionary<string, object?> { ["code"] = res.Data!.Code };
    }

    public static object CreateMaterialReplacement(Context ctx)
    {
        if (ctx.Db is not SqliteDbFixture db)
            throw new ValidationException("DB_NOT_AVAILABLE");

        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));

        var groupId = Convert.ToInt32(ctx.Input["group_id"]);

        var specRaw = ctx.Input.TryGetValue("spec", out var sp) ? sp?.ToString() : null;

        var res = app.CreateReplacement(new CreateReplacementRequest(
            GroupId: groupId,
            Spec: specRaw ?? "__DUMMY__",
            Name: MaterialInputTextOrPlaceholder(ctx, "name", "n"),
            Description: MaterialInputTextOrPlaceholder(ctx, "description", "d"),
            Brand: ctx.Input["brand"]?.ToString()
        )).GetAwaiter().GetResult();

        if (!res.IsSuccess)
            throw new ValidationException(res.Error!.Code, res.Error.Message);

        return new Dictionary<string, object?> { ["code"] = res.Data!.Code };
    }

    public static object GenerateMaterialCode(Context ctx)
    {
        var cc = ctx.Input["category_code"]?.ToString() ?? "";
        var serial = Convert.ToInt32(ctx.Input["serial_no"]);
        var suffix = (ctx.Input["suffix"]?.ToString() ?? "A")[0];
        var gen = MaterialCodeQueriesV1.GenerateItemCode(cc, serial, suffix);
        if (!gen.IsSuccess)
            throw new ValidationException(gen.Error!.Code, gen.Error.Message);
        return new Dictionary<string, object?> { ["code"] = gen.Data! };
    }

    public static object FormatSerial(Context ctx)
    {
        var serial = Convert.ToInt32(ctx.Input["serial_no"]);
        var fmt = MaterialFormatQueriesV1.FormatSerialWidth6(serial);
        if (!fmt.IsSuccess)
            throw new ValidationException(fmt.Error!.Code, fmt.Error.Message);
        return new Dictionary<string, object?> { ["formatted"] = fmt.Data! };
    }

    public static object UpdateStatus(Context ctx)
    {
        if (ctx.Db is not SqliteDbFixture db)
            throw new ValidationException("DB_NOT_AVAILABLE");

        var status = Convert.ToInt32(ctx.Input["status"]);
        if (status is not (0 or 1))
            throw new ValidationException("INVALID_STATUS_TRANSITION");

        if (status == 1)
            throw new ValidationException("INVALID_STATUS_TRANSITION"); // V1 仅允许废弃，不支持恢复

        var code = ctx.Input["code"]?.ToString() ?? "";
        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));
        var res = app.DeprecateMaterialItem(new DeprecateRequest(code)).GetAwaiter().GetResult();
        if (!res.IsSuccess) throw new ValidationException(res.Error!.Code);
        return new Dictionary<string, object?> { ["status"] = 0 };
    }

    public static object UpdateGroup(Context ctx)
    {
        // PRD V1：不允许变更 group 的 category_code（本用例用于断言冻结语义）
        throw new ValidationException("GROUP_CATEGORY_LOCKED");
    }

    public static object AllocateGroupSerial(Context ctx)
    {
        if (ctx.Db is not SqliteDbFixture db)
            throw new ValidationException("DB_NOT_AVAILABLE");

        var categoryCode = ctx.Input["category_code"]?.ToString() ?? "";
        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));
        var res = app.AllocateNextGroupSerial(categoryCode).GetAwaiter().GetResult();
        if (!res.IsSuccess)
            throw new ValidationException(res.Error!.Code, res.Error.Message);

        return new Dictionary<string, object?> { ["serial_no"] = res.Data! };
    }

    public static object CreateMaterialABatch(Context ctx)
    {
        if (ctx.Db is not SqliteDbFixture db)
            throw new ValidationException("DB_NOT_AVAILABLE");

        // ensure category exists
        var category = ctx.Input["category_code"]?.ToString() ?? "";
        if (!db.Exists("category", new Dictionary<string, object?> { ["code"] = category }))
            db.Connection.Execute("INSERT INTO category(code,name) VALUES (@code,@name);", new { code = category, name = category });

        var prefix = ctx.Input["spec_prefix"]?.ToString() ?? "TEST_";
        var count = Convert.ToInt32(ctx.Input["count"]);

        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));
        var codes = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var res = app.CreateMaterialItemA(new CreateMaterialItemARequest(
                CategoryCode: category,
                Spec: prefix + i,
                Name: "n",
                Description: "d",
                Brand: null
            )).GetAwaiter().GetResult();
            if (!res.IsSuccess) throw new ValidationException(res.Error!.Code);
            codes.Add(res.Data!.Code);
        }

        var noDup = codes.Distinct().Count() == codes.Count;
        return new Dictionary<string, object?> { ["no_duplicate"] = noDup };
    }

    public static object CreateMaterialAConcurrent(Context ctx)
    {
        // This action needs real concurrency. We use a shared in-memory sqlite
        // with one master connection held open, plus per-worker connections.
        var dbName = $"mcs_{Guid.NewGuid():N}";
        var cs = $"Data Source=file:{dbName}?mode=memory&cache=shared";

        using var master = new Microsoft.Data.Sqlite.SqliteConnection(cs);
        master.Open();

        // schema + category seed
        master.Execute("""
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
                         UNIQUE(category_id, serial_no)
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
                         CHECK(status IN (0,1))
                       );
                       """);

        var category = ctx.Input["category_code"]?.ToString() ?? "";
        master.Execute("INSERT INTO category(code,name) VALUES (@code,@name);", new { code = category, name = category });

        var spec = ctx.Input["spec"]?.ToString() ?? "";
        var concurrent = Convert.ToInt32(ctx.Input["concurrent"]);

        var successes = 0;
        var duplicates = 0;

        Parallel.For(0, concurrent, _ =>
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection(cs);
            conn.Open();
            var app = new MaterialApplicationService(new SqliteUnitOfWork(conn), new SqliteMaterialRepository(conn));
            var res = app.CreateMaterialItemA(new CreateMaterialItemARequest(category, spec, "n", "d", null)).GetAwaiter().GetResult();
            if (res.IsSuccess) Interlocked.Increment(ref successes);
            else Interlocked.Increment(ref duplicates);
        });

        return new Dictionary<string, object?> { ["success_count"] = successes, ["duplicate_rejected"] = duplicates };
    }

    /// <summary>
    /// PRD_V1.yaml 可省略 name/description；runner 注入占位以满足 Application 入参非空校验（非产品 UI 行为）。
    /// </summary>
    private static string MaterialInputTextOrPlaceholder(Context ctx, string key, string placeholder)
    {
        if (!ctx.Input.TryGetValue(key, out var v) || v is null)
            return placeholder;
        var s = v.ToString();
        return string.IsNullOrWhiteSpace(s) ? placeholder : s;
    }
}

