namespace MaterialCodingSystem.Domain.ValueObjects;

public readonly record struct SpecNormalized
{
    public string Value { get; }

    public SpecNormalized(string value)
    {
        Value = value ?? string.Empty;
    }

    public override string ToString() => Value;
}

