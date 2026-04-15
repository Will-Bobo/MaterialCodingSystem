using System;
using System.Threading;
using System.Threading.Tasks;

namespace MaterialCodingSystem.Application;

/// <summary>
/// 运维类操作（Export / AutoBackup / Restore）统一互斥门禁。
/// 仅保护运维操作，不影响业务查询/写入用例。
/// </summary>
public sealed class MaintenanceOperationGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await action();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

