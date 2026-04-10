namespace MaterialCodingSystem.Validation.core;

public class DbFixture
{
    private readonly Dictionary<string, object?> _db = new();

    public void Reset()
    {
        _db.Clear();
    }

    public void Seed(Dictionary<string, object?>? data)
    {
        if (data == null) return;

        foreach (var table in data)
        {
            _db[table.Key] = table.Value;
        }
    }

    public object? Get(string key)
    {
        return _db.TryGetValue(key, out var v) ? v : null;
    }
}