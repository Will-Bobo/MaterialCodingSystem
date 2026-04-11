namespace MaterialCodingSystem.Domain.ValueObjects;

public readonly record struct CategoryCode
{
    public string Value { get; }

    public CategoryCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new Services.DomainException("VALIDATION_ERROR", "category_code is required.");
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}

