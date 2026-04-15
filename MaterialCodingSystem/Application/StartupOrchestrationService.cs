using System;
using System.Threading.Tasks;

namespace MaterialCodingSystem.Application;

public sealed class StartupOrchestrationService
{
    private readonly DatabaseBackupService _backup;

    public StartupOrchestrationService(DatabaseBackupService backup)
    {
        _backup = backup;
    }

    public async Task OnAppStartedAsync()
    {
        // V1: 自动备份无 UI，启动完成后延迟触发一次；失败不影响主流程。
        try
        {
            await Task.Delay(3000).ConfigureAwait(false);
            _ = await _backup.CreateAutoBackup().ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }
    }
}

