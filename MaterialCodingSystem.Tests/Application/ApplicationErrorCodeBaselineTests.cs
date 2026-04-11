using System.Reflection;
using MaterialCodingSystem.Application;

namespace MaterialCodingSystem.Tests.Application;

/// <summary>
/// 错误码「清单 vs 实现」基线（Phase 2）。SPEC_DUPLICATE / CODE_CONFLICT_RETRY 行为由
/// <see cref="CreateMaterialItemATests"/>、<see cref="CreateReplacementTests"/> 覆盖。
/// </summary>
public class ApplicationErrorCodeBaselineTests
{
    /// <summary>
    /// 部分检查清单使用「SUFFIX_CONFLICT」；当前 <see cref="ErrorCodes"/> 未定义，
    /// 后缀插入冲突重试耗尽时返回 <see cref="ErrorCodes.CODE_CONFLICT_RETRY"/>（见 CreateReplacementTests）。
    /// </summary>
    [Fact]
    public void BehaviorDeviation_ChecklistSuffixConflict_NotDefined_ErrorCodesUsesCodeConflictRetryForSuffixRetryExhaustion()
    {
        var names = typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("SUFFIX_CONFLICT", names);
    }
}
