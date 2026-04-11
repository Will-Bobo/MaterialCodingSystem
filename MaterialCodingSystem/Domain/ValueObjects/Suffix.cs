namespace MaterialCodingSystem.Domain.ValueObjects;

public readonly record struct Suffix
{
    public char Value { get; }

    public Suffix(char value)
    {
        if (value is < 'A' or > 'Z')
        {
            throw new Services.DomainException("VALIDATION_ERROR", "suffix must be A-Z.");
        }

        Value = value;
    }

    public override string ToString() => Value.ToString();
}

