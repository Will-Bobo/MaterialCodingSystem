using MaterialCodingSystem.Application.Contracts;
using MaterialCodingSystem.Domain.Services;

namespace MaterialCodingSystem.Application;

/// <summary>
/// PRD V1：无持久化依赖的只读查询（供 Validation 等经 Application 入口调用 Domain 编码规则）。
/// </summary>
public static class MaterialCodeQueriesV1
{
    public static Result<string> GenerateItemCode(string categoryCode, int serialNo, char suffix)
    {
        try
        {
            return Result<string>.Ok(CodeGenerator.GenerateItemCode(categoryCode, serialNo, suffix));
        }
        catch (DomainException ex)
        {
            return Result<string>.Fail(ex.Code, ex.Message);
        }
    }
}
