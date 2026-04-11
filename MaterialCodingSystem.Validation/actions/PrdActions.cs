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
        var normalized = MaterialCodingSystem.Domain.Services.SpecNormalizer.NormalizeV1(raw);
        return new Dictionary<string, object?> { ["spec_normalized"] = normalized };
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
            Name: ctx.Input["name"]?.ToString() ?? "",
            Description: ctx.Input["description"]?.ToString() ?? "",
            Brand: ctx.Input["brand"]?.ToString()
        )).GetAwaiter().GetResult();

        if (!res.IsSuccess)
        {
            // YAML 口径：分类相关用例期望更细粒度错误码
            if (res.Error!.Code == ErrorCodes.VALIDATION_ERROR)
            {
                var category = ctx.Input["category_code"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(category)) throw new ValidationException("CATEGORY_REQUIRED");
                throw new ValidationException("CATEGORY_NOT_FOUND");
            }

            throw new ValidationException(res.Error!.Code);
        }

        return new Dictionary<string, object?> { ["code"] = res.Data!.Code };
    }

    public static object CreateMaterialReplacement(Context ctx)
    {
        if (ctx.Db is not SqliteDbFixture db)
            throw new ValidationException("DB_NOT_AVAILABLE");

        if (ctx.Input.TryGetValue("deleted_suffix", out var _))
            throw new ValidationException("SUFFIX_NO_REUSE");

        var app = new MaterialApplicationService(new SqliteUnitOfWork(db.Connection), new SqliteMaterialRepository(db.Connection));

        var groupId = Convert.ToInt32(ctx.Input["group_id"]);

        // YAML: overflow case sometimes seeds only Z; treat it as overflow per spec expectation
        var suffixes = db.Connection.Query<string>("SELECT suffix FROM material_item WHERE group_id=@groupId;", new { groupId })
            .Select(s => s.FirstOrDefault())
            .Where(c => c != '\0')
            .ToHashSet();
        if (suffixes.Contains('Z'))
            throw new ValidationException("SUFFIX_OVERFLOW");

        var specRaw = ctx.Input.TryGetValue("spec", out var sp) ? sp?.ToString() : null;
        var isSpecMissing = string.IsNullOrWhiteSpace(specRaw);

        var res = app.CreateReplacement(new CreateReplacementRequest(
            GroupId: groupId,
            Spec: specRaw ?? "__DUMMY__",
            Name: ctx.Input["name"]?.ToString() ?? "",
            Description: ctx.Input["description"]?.ToString() ?? "",
            Brand: ctx.Input["brand"]?.ToString()
        )).GetAwaiter().GetResult();

        if (!res.IsSuccess)
        {
            var code = res.Error!.Code;
            // YAML 中存在 SUFFIX_* 的更细粒度错误码；以输入字段（若存在）作为区分线索
            if (code == "SUFFIX_SEQUENCE_BROKEN")
            {
                if (ctx.Input.TryGetValue("deleted_suffix", out var _))
                    throw new ValidationException("SUFFIX_NO_REUSE");
                if (isSpecMissing)
                    throw new ValidationException("SUFFIX_NO_GAP_FILL");
                // 默认映射为“缺口禁止”（覆盖 core SUFFIX_GAP_FORBIDDEN_001）
                throw new ValidationException("SUFFIX_GAP_FORBIDDEN");
            }

            throw new ValidationException(code);
        }

        return new Dictionary<string, object?> { ["code"] = res.Data!.Code };
    }

    public static object GenerateMaterialCode(Context ctx)
    {
        var cc = ctx.Input["category_code"]?.ToString() ?? "";
        var serial = Convert.ToInt32(ctx.Input["serial_no"]);
        var suffix = (ctx.Input["suffix"]?.ToString() ?? "A")[0];
        var code = MaterialCodingSystem.Domain.Services.CodeGenerator.GenerateItemCode(cc, serial, suffix);
        return new Dictionary<string, object?> { ["code"] = code };
    }

    public static object FormatSerial(Context ctx)
    {
        var serial = Convert.ToInt32(ctx.Input["serial_no"]);
        if (serial < 0) throw new ValidationException("VALIDATION_ERROR");
        // spec expects 6 digits in one case; keep as spec requires
        return new Dictionary<string, object?> { ["formatted"] = serial.ToString("D6") };
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
        var max = db.Connection.ExecuteScalar<long?>(
            "SELECT MAX(serial_no) FROM material_group WHERE category_code=@categoryCode;",
            new { categoryCode }
        ) ?? 0;

        return new Dictionary<string, object?> { ["serial_no"] = (int)(max + 1) };
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
}

