using System.Runtime.Versioning;

namespace TrafficSense;

// Create and dispose on the application's main thread: Windows mutex ownership
// belongs to that thread. The event is separate so activation never waits on it.
[SupportedOSPlatform("windows")]
internal sealed class SingleInstanceGate : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showRequested;
    private RegisteredWaitHandle? _listener;
    private bool _disposed;

    public bool IsPrimary { get; }

    public SingleInstanceGate(string name)
    {
        _mutex = new Mutex(false, name + ".Mutex");
        try
        {
            // Create the event before electing the owner, so a launch during
            // startup can leave a pending request until the UI is ready.
            _showRequested = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Show");
            try { IsPrimary = _mutex.WaitOne(0); }
            catch (AbandonedMutexException) { IsPrimary = true; }
        }
        catch
        {
            _showRequested?.Dispose();
            _mutex.Dispose();
            throw;
        }
    }

    public void Listen(Action showWidget)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsPrimary || _listener != null)
            throw new InvalidOperationException("Only the primary instance can listen, once.");
        _listener = ThreadPool.RegisterWaitForSingleObject(_showRequested,
            (_, _) => showWidget(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void NotifyPrimary()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _showRequested.Set();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _listener?.Unregister(null);
        _showRequested.Dispose();
        if (IsPrimary) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
