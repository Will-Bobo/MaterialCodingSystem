namespace MaterialCodingSystem.Validation.core;

public abstract record AssertionSpec
{
    public sealed record ExpectError(bool ShouldThrow, string? Code) : AssertionSpec;
    public sealed record ExpectResultEquals(object? Expected) : AssertionSpec;
}
