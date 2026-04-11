namespace MaterialCodingSystem.Validation.core;

public class ValidationSpec
{
    public string? SpecVersion { get; set; }
    public Dictionary<string, object?>? Meta { get; set; }
    public List<ValidationCase> Cases { get; set; } = new();
}

public class ValidationCase
{
    public string Id { get; set; } = "";
    public string? Title { get; set; }
    public string? Type { get; set; }

    public Given? Given { get; set; }
    public When? When { get; set; }
    public Then? Then { get; set; }

    public bool Replay { get; set; }
    public bool Deterministic { get; set; }
}

public class Given
{
    public Dictionary<string, object?>? Db { get; set; }
    public Dictionary<string, object?>? Input { get; set; }
}

public class When
{
    public string Action { get; set; } = "";
    public Dictionary<string, object?>? Context { get; set; }
}

public class Then
{
    public Dictionary<string, object?>? Output { get; set; }
    public Dictionary<string, object?>? Db { get; set; }
    public ErrorExpectation? Error { get; set; }
}

public class ErrorExpectation
{
    public bool? ShouldThrow { get; set; }
    public string? Code { get; set; }
}

public sealed class InputModel
{
    private readonly Dictionary<string, object?> _data;

    public InputModel(Dictionary<string, object?>? data)
    {
        _data = data ?? new Dictionary<string, object?>();
    }

    public object? this[string key]
    {
        get => _data.TryGetValue(key, out var v) ? v : null;
        set => _data[key] = value;
    }

    public bool TryGetValue(string key, out object? value) => _data.TryGetValue(key, out value);

    public Dictionary<string, object?> ToDictionary() => new(_data);
}

public sealed class Context
{
    public DbFixture Db { get; }
    public InputModel Input { get; }

    public Context(DbFixture db, InputModel input)
    {
        Db = db ?? throw new ArgumentNullException(nameof(db));
        Input = input ?? throw new ArgumentNullException(nameof(input));
    }
}