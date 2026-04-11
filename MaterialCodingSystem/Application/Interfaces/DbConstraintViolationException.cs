namespace MaterialCodingSystem.Application.Interfaces;

public sealed class DbConstraintViolationException : Exception
{
    public string Constraint { get; }

    public DbConstraintViolationException(string constraint, string message) : base(message)
    {
        Constraint = constraint;
    }
}

