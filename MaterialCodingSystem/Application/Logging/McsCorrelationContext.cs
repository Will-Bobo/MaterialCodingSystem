namespace MaterialCodingSystem.Application.Logging;

/// <summary>
/// 顶层用例 correlation_id（AsyncLocal，随 async/await 流动）。
/// 嵌套调用复用同一 ID；仅在 Current 为空时由 helper 生成。
/// </summary>
public static class McsCorrelationContext
{
    private static readonly AsyncLocal<string?> Store = new();

    public static string? Current => Store.Value;

    /// <summary>若无 correlation，生成短 GUID（12 hex）并写入当前异步上下文。</summary>
    public static void EnsureRootCorrelationId()
    {
        if (Store.Value != null)
            return;
        Store.Value = Guid.NewGuid().ToString("N")[..12];
    }
}
