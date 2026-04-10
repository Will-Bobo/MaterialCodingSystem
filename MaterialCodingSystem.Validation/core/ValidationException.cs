namespace MaterialCodingSystem.Validation.core;

public class ValidationException : Exception
{
    public string Code { get; }

    public ValidationException(string code)
        : base(code)
    {
        Code = code;
    }

    public ValidationException(string code, string? message)
        : base(message)
    {
        Code = code;
    }

    public ValidationException(string code, string? message, Exception? innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}
