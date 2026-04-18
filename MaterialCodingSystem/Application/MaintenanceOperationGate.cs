using System.Diagnostics;
using MaterialCodingSystem.Application.Logging;
using Microsoft.Extensions.Logging;

namespace MaterialCodingSystem.Application;

/// <summary>
/// 运维类操作（Export / AutoBackup / Restore）统一互斥门禁。
/// 仅保护运维操作，不影响业务查询/写入用例。
/// </summary>
public sealed class MaintenanceOperationGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<MaintenanceOperationGate> _logger;

    public MaintenanceOperationGate(ILogger<MaintenanceOperationGate>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MaintenanceOperationGate>.Instance;
    }

    public async Task<T> RunAsync<T>(string gateName, Func<Task<T>> action, CancellationToken ct = default)
    {
        var immediate = await _semaphore.WaitAsync(TimeSpan.Zero, ct).ConfigureAwait(false);
        if (!immediate)
        {
            var waitSw = Stopwatch.StartNew();
            await _semaphore.WaitAsync(ct).ConfigureAwait(false);
            waitSw.Stop();
            McsLoggingExtensions.LogMaintenanceGateBlocked(_logger, gateName, waitSw.ElapsedMilliseconds);
        }

        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
