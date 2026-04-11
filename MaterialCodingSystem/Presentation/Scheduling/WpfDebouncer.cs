using System.Windows.Threading;

namespace MaterialCodingSystem.Presentation.Scheduling;

public sealed class WpfDebouncer : IDebouncer, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<object, DispatcherTimer> _timers = new();

    public WpfDebouncer(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Debounce(object key, TimeSpan delay, Func<CancellationToken, Task> work)
    {
        if (_dispatcher.HasShutdownStarted) return;

        _dispatcher.Invoke(() =>
        {
            if (_timers.TryGetValue(key, out var old))
            {
                old.Stop();
                _timers.Remove(key);
            }

            var timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = delay
            };

            timer.Tick += (_, _) =>
            {
                timer.Stop();
                _timers.Remove(key);
                _ = RunWorkAsync(work);
            };

            _timers[key] = timer;
            timer.Start();
        });
    }

    public void Dispose()
    {
        _dispatcher.Invoke(() =>
        {
            foreach (var t in _timers.Values)
                t.Stop();
            _timers.Clear();
        });
    }

    private static async Task RunWorkAsync(Func<CancellationToken, Task> work)
    {
        try
        {
            await work(default);
        }
        catch
        {
            // VM 内应自行处理；此处避免未观察异常
        }
    }
}
