namespace MaterialCodingSystem.Domain.Services;

public readonly record struct ParsedMaterialCode(string NormalizedCode, string CategoryCode, int SerialNo, char Suffix);

public static class MaterialCodeParser
{
    /// <summary>
    /// Parse code formatted as: {categoryCode}{serialNo:D7}{suffix}, suffix A-Z.
    /// Normalization: Trim + Uppercase.
    /// </summary>
    public static ParsedMaterialCode ParseExistingCode(string? raw)
    {
        var code = (raw ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length < 1 + 7 + 1)
            throw new DomainException("VALIDATION_ERROR", "existing_code format invalid.");

        var suffix = code[^1];
        if (suffix is < 'A' or > 'Z')
            throw new DomainException("VALIDATION_ERROR", "existing_code suffix invalid.");

        var digits = code.Substring(code.Length - 8, 7);
        if (!int.TryParse(digits, out var serialNo) || serialNo < 1)
            throw new DomainException("VALIDATION_ERROR", "existing_code serial_no invalid.");

        var categoryCode = code.Substring(0, code.Length - 8);
        if (string.IsNullOrWhiteSpace(categoryCode))
            throw new DomainException("VALIDATION_ERROR", "existing_code category_code invalid.");

        return new ParsedMaterialCode(code, categoryCode, serialNo, suffix);
    }

    public static ParsedMaterialCode ParseExistingCodeWithCategory(string? raw, string categoryCode)
    {
        var code = (raw ?? string.Empty).Trim().ToUpperInvariant();
        var cat = (categoryCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(cat))
            throw new DomainException("VALIDATION_ERROR", "category_code is required.");

        if (!code.StartsWith(cat, StringComparison.Ordinal))
            throw new DomainException("VALIDATION_ERROR", "existing_code category_code mismatch.");

        var tail = code.Substring(cat.Length);
        if (tail.Length != 8)
            throw new DomainException("VALIDATION_ERROR", "existing_code format invalid.");

        var digits = tail.Substring(0, 7);
        if (!int.TryParse(digits, out var serialNo) || serialNo < 1)
            throw new DomainException("VALIDATION_ERROR", "existing_code serial_no invalid.");

        var suffix = tail[7];
        if (suffix is < 'A' or > 'Z')
            throw new DomainException("VALIDATION_ERROR", "existing_code suffix invalid.");

        return new ParsedMaterialCode(code, cat, serialNo, suffix);
    }
}

