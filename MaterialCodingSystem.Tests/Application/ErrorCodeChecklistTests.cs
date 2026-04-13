using MaterialCodingSystem.Application;

namespace MaterialCodingSystem.Tests.Application;

/// <summary>
/// 与需求清单对齐的错误码基线（具体断言仍分布在各用例类中，此处集中「清单关键词 → 实现事实」）。
/// </summary>
public class ErrorCodeChecklistTests
{
    [Fact]
    public void Checklist_SpecDuplicate_ErrorCode_Constant_Is_SPEC_DUPLICATE()
    {
        Assert.Equal("SPEC_DUPLICATE", ErrorCodes.SPEC_DUPLICATE);
    }

    /// <summary>
    /// 清单常见表述「suffix conflict」；产品中后缀插入冲突重试耗尽为 <see cref="ErrorCodes.CODE_CONFLICT_RETRY"/>，
    /// 且不存在 <c>SUFFIX_CONFLICT</c> 常量 — 行为偏差，见 <see cref="ApplicationErrorCodeBaselineTests"/> 与
    /// <see cref="CreateReplacementTests.CreateReplacement_WhenSuffixConflict_Exceeds3_ReturnsCodeConflictRetry"/>。
    /// </summary>
    [Fact]
    public void Checklist_SuffixConflict_ProductUses_CODE_CONFLICT_RETRY_Not_SUFFIX_CONFLICT()
    {
        Assert.Equal("CODE_CONFLICT_RETRY", ErrorCodes.CODE_CONFLICT_RETRY);
        Assert.DoesNotContain(
            "SUFFIX_CONFLICT",
            typeof(ErrorCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Select(f => f.Name));
    }

    /// <summary>
    /// group serial 并发冲突重试成功：见
    /// <see cref="CreateMaterialItemATests.CreateA_WhenSerialConflict_RetriesUpTo3Times_ThenSucceeds"/> 与
    /// <see cref="Phase2InMemorySharedCacheConcurrencyIntegrationTests.ParallelCreateA_DifferentSpecs_AllSucceed_UniqueSerials"/>。
    /// </summary>
    [Fact]
    public void Checklist_GroupSerialRetry_Documented_In_CreateMaterialItemA_and_ParallelIntegration()
    {
        Assert.Equal("CODE_CONFLICT_RETRY", ErrorCodes.CODE_CONFLICT_RETRY);
    }
}
