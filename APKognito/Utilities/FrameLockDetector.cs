using System.Diagnostics;
using System.Windows.Threading;

namespace APKognito.Utilities;

public sealed class FrameLockDetector : IDisposable
{
    private readonly Timer _probeTimer;
    private readonly TimeSpan _maxFrameTime = TimeSpan.FromMilliseconds(500);

    private long _lastHeartbeatTicks;

    public FrameLockDetector(Dispatcher dispatcher)
    {
        _lastHeartbeatTicks = Stopwatch.GetTimestamp();

        dispatcher.Hooks.DispatcherInactive += (s, e) =>
        {
            _ = Interlocked.Exchange(ref _lastHeartbeatTicks, Stopwatch.GetTimestamp());
        };

        _probeTimer = new Timer(ProbeDispatcher, null, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(200));
    }

    private void ProbeDispatcher(object? state)
    {
        long lastTicks = Interlocked.Read(ref _lastHeartbeatTicks);
        long currentTicks = Stopwatch.GetTimestamp();

        double elapsedMs = (currentTicks - lastTicks) * 1000.0 / Stopwatch.Frequency;

        if (elapsedMs > _maxFrameTime.TotalMilliseconds)
        {
            FileLogger.LogWarning($"UI frame time flagged over {_maxFrameTime.TotalMilliseconds:F0}ms, took {elapsedMs:F0}ms.");

            _ = Interlocked.Exchange(ref _lastHeartbeatTicks, currentTicks);
        }
    }

    public void Dispose()
    {
        _probeTimer.Dispose();
    }
}
