using System;

namespace MaterialCodingSystem.Presentation.Services;

/// <summary>
/// 恢复数据库成功后，UI 进入“只读锁定态”，直到应用退出重启。
/// 最小实现：通过静态状态让命令统一不可执行，避免继续发起任何数据库操作。
/// </summary>
public static class ReadOnlyLock
{
    private static bool _isLocked;
    public static bool IsLocked => _isLocked;

    public static event EventHandler? Changed;

    public static void Enter()
    {
        if (_isLocked) return;
        _isLocked = true;
        Changed?.Invoke(null, EventArgs.Empty);
    }
}

