namespace MaterialCodingSystem.Presentation.Scheduling;

public interface IDebouncer
{
    /// <summary>同 key 会取消上一次未执行的调度。</summary>
    void Debounce(object key, TimeSpan delay, Func<CancellationToken, Task> work);
}
