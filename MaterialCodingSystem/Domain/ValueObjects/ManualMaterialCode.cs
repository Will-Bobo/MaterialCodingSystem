using System.Text.RegularExpressions;

using MaterialCodingSystem.Domain.Services;

namespace MaterialCodingSystem.Domain.ValueObjects;

/// <summary>
/// Manual 模式手工输入的物料编码：负责 trim/upper、正则校验与解析。
/// 规则：^[A-Z]{3}[0-9]{7}[A-Z]$，serial_no >= 0（兼容历史 0000000）。
/// </summary>
public sealed class ManualMaterialCode
{
    // 约束：3位大写分类 + 7位数字流水 + 1位大写后缀
    private static readonly Regex Pattern = new("^[A-Z]{3}[0-9]{7}[A-Z]$", RegexOptions.Compiled);

    public string NormalizedCode { get; }
    public CategoryCode CategoryCode { get; }
    public int SerialNo { get; }
    public Suffix Suffix { get; }

    private ManualMaterialCode(string normalizedCode, CategoryCode categoryCode, int serialNo, Suffix suffix)
    {
        NormalizedCode = normalizedCode;
        CategoryCode = categoryCode;
        SerialNo = serialNo;
        Suffix = suffix;
    }

    public static ManualMaterialCode Parse(string raw)
    {
        if (raw is null)
        {
            throw new DomainException("CODE_FORMAT_INVALID", "code is required.");
        }

        var normalized = raw.Trim().ToUpperInvariant();
        if (!Pattern.IsMatch(normalized))
        {
            throw new DomainException("CODE_FORMAT_INVALID", "code format invalid.");
        }

        var category = normalized[..3];
        var serialText = normalized.Substring(3, 7);
        var suffixChar = normalized[10];

        if (!int.TryParse(serialText, out var serialNo) || serialNo < 0)
        {
            throw new DomainException("CODE_FORMAT_INVALID", "serial_no invalid.");
        }

        // suffix 允许 A-Z；Suffix ValueObject 会做范围校验
        var suffix = new Suffix(suffixChar);
        var categoryCode = new CategoryCode(category);

        return new ManualMaterialCode(normalized, categoryCode, serialNo, suffix);
    }
}

