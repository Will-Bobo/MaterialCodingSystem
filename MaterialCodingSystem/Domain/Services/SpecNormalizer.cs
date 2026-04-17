using System.Text;

namespace MaterialCodingSystem.Domain.Services;

public static class SpecNormalizer
{
    // PRD V1：仅允许 trim + collapse spaces + uppercase（禁止语义解析/单位转换等）
    public static string NormalizeV1(string? description)
    {
        if (description is null)
        {
            return string.Empty;
        }

        // V1.4 增量：中文逗号统一转英文逗号（公共 Normalize 模块统一生效）
        var replacedComma = description.Replace('，', ',');

        var trimmed = replacedComma.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(trimmed.Length);
        var prevWasWhitespace = false;

        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevWasWhitespace)
                {
                    sb.Append(' ');
                    prevWasWhitespace = true;
                }

                continue;
            }

            sb.Append(char.ToUpperInvariant(ch));
            prevWasWhitespace = false;
        }

        return sb.ToString();
    }
}

