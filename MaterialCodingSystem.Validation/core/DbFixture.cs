namespace MaterialCodingSystem.Validation.core;

public abstract class DbFixture
{
    public abstract void Reset();
    public abstract void Seed(Dictionary<string, object?>? data);
    public virtual object? Get(string key) => null;
}