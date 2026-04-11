namespace MaterialCodingSystem.Domain.Services;

public static class CodeGenerator
{
    public static string GenerateItemCode(string categoryCode, int serialNo, char suffix)
    {
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            throw new DomainException("VALIDATION_ERROR", "categoryCode is required.");
        }

        if (serialNo <= 0)
        {
            throw new DomainException("VALIDATION_ERROR", "serialNo must be positive.");
        }

        if (suffix is < 'A' or > 'Z')
        {
            throw new DomainException("VALIDATION_ERROR", "suffix must be A-Z.");
        }

        return $"{categoryCode}{serialNo:D7}{suffix}";
    }
}

