using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Domain.Services;

namespace MaterialCodingSystem.Application;

/// <summary>
/// PRD V1：无持久化依赖的只读查询（供 Validation / UI 等经 Application 入口调用 Domain 规则）。
/// </summary>
public static class MaterialSpecQueriesV1
{
    /// <summary>description → spec_normalized（trim + 空白折叠 + 大写）。</summary>
    public static Result<string> NormalizeDescriptionToSpecNormalized(string? description) =>
        Result<string>.Ok(SpecNormalizer.NormalizeV1(description));
}
