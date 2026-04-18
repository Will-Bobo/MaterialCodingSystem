using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MaterialCodingSystem.Application.Logging;
using Microsoft.Extensions.Logging;

namespace MaterialCodingSystem.Application;

public sealed class StartupOrchestrationService
{
    private readonly DatabaseBackupService _backup;
    private readonly ILogger<StartupOrchestrationService> _logger;

    public StartupOrchestrationService(DatabaseBackupService backup, ILogger<StartupOrchestrationService>? logger = null)
    {
        _backup = backup;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StartupOrchestrationService>.Instance;
    }

    public async Task OnAppStartedAsync()
    {
        McsCorrelationContext.EnsureRootCorrelationId();
        McsLoggingExtensions.LogStart(_logger, McsActions.SystemAppStarted, null);
        var sw = Stopwatch.StartNew();
        try
        {
            await Task.Delay(3000).ConfigureAwait(false);
            _ = await _backup.CreateAutoBackup().ConfigureAwait(false);
            sw.Stop();
            McsLoggingExtensions.LogSuccess(_logger, McsActions.SystemAppStarted, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            McsLoggingExtensions.LogFailed(_logger, McsActions.SystemAppStarted, ErrorCodes.INTERNAL_ERROR, sw.ElapsedMilliseconds);
            McsLoggingExtensions.LogException(_logger, ex, McsActions.SystemAppStarted, ErrorCodes.INTERNAL_ERROR);
        }
    }
}

