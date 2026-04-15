using MaterialCodingSystem.Application.Interfaces;
using System.Windows.Input;

namespace MaterialCodingSystem.Presentation.Services;

public sealed class WpfRestoreReadOnlyLockNotifier : IRestoreReadOnlyLockNotifier
{
    public void EnterReadOnlyLock()
    {
        ReadOnlyLock.Enter();
        // 触发 WPF 重新查询 CanExecute（并不依赖 CommandManager 绑定，但可加速 UI 反馈）。
        CommandManager.InvalidateRequerySuggested();
    }
}

