namespace MaterialCodingSystem.Validation.core;

public sealed class ExecutionPlan
{
    public string ActionName { get; init; } = "";
    public Dictionary<string, object?> Input { get; init; } = new();
    public List<AssertionSpec> Assertions { get; init; } = new();
    public bool Replay { get; init; }
    public bool Deterministic { get; init; }
    public DbSeed? Seed { get; init; }
}

public sealed class DbSeed
{
    public Dictionary<string, object?> Data { get; init; } = new();
}
