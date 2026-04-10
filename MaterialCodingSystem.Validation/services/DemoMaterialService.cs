using MaterialCodingSystem.Validation.core;
using MaterialCodingSystem.Validation.infrastructure;

namespace MaterialCodingSystem.Validation.services;

/// <summary>
/// 演示：业务式服务仅通过 IDbContextProvider 获取 DbContext，禁止在方法内 new AppDbContext。
/// 仅用于 Validation 工程内证明「注入 Provider + Action 调用服务」的闭环。
/// </summary>
public sealed class DemoMaterialService
{
    private readonly IDbContextProvider _dbProvider;

    public DemoMaterialService(IDbContextProvider dbProvider)
    {
        _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
    }

    public object Create(Context ctx)
    {
        var name = ctx.Input["name"]?.ToString() ?? "";

        using var db = _dbProvider.Create();
        db.DemoRecords.Add(new DemoRecord { Name = name });
        db.SaveChanges();

        var count = db.DemoRecords.Count();
        return new Dictionary<string, object?> { ["ok"] = true, ["row_count"] = count };
    }
}
