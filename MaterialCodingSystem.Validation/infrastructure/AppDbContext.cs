using Microsoft.EntityFrameworkCore;

namespace MaterialCodingSystem.Validation.infrastructure;

/// <summary>
/// 演示用 DbContext，仅存在于 Validation 项目内，不代表主业务真实模型。
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<DemoRecord> DemoRecords => Set<DemoRecord>();
}

public sealed class DemoRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
