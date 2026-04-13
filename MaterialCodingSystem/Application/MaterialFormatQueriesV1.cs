using MaterialCodingSystem.Application.Contracts;

namespace MaterialCodingSystem.Application;

/// <summary>
/// PRD V1：与持久化无关的格式化辅助（供 Validation 等经 Application 入口使用）。
/// </summary>
public static class MaterialFormatQueriesV1
{
    /// <summary>非负流水号格式化为 6 位十进制（D6）。</summary>
    public static Result<string> FormatSerialWidth6(int serialNo)
    {
        if (serialNo < 0)
        {
            return Result<string>.Fail(ErrorCodes.VALIDATION_ERROR, "serial_no must be non-negative.");
        }

        return Result<string>.Ok(serialNo.ToString("D6"));
    }
}
