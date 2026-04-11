namespace MaterialCodingSystem.Domain.ValueObjects;

public readonly record struct Spec
{
    public string Value { get; }

    public Spec(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new Services.DomainException("VALIDATION_ERROR", "spec is required.");
        }

        Value = value;
    }

    public override string ToString() => Value;
}

