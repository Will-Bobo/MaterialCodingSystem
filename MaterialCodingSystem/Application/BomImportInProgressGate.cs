using System.Collections.Concurrent;

namespace MaterialCodingSystem.Application;

/// <summary>
/// 轻量并发保护：同 finished_code + version 的导入过程互斥（进程内）。
/// </summary>
public sealed class BomImportInProgressGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public bool TryEnter(string key, out SemaphoreSlim semaphore)
    {
        semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        return semaphore.Wait(0);
    }

    public void Exit(string key, SemaphoreSlim semaphore)
    {
        semaphore.Release();
        // keep semaphore cached to avoid churn
    }
}

