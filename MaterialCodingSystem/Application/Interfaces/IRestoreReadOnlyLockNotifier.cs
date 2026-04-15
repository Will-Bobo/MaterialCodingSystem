namespace MaterialCodingSystem.Application.Interfaces;

/// <summary>
/// Presentation 层“恢复成功后立即只读锁定态”的最小契约钩子。
/// Application 不依赖具体 UI，仅在 Restore 成功时通知进入锁定态，直到应用退出重启。
/// </summary>
public interface IRestoreReadOnlyLockNotifier
{
    void EnterReadOnlyLock();
}

