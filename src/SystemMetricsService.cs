using System.Diagnostics;

namespace TrafficSense;

internal sealed class SystemMetricsService : IDisposable
{
    private readonly SystemMetric _enabled;
    private readonly Func<SystemMetric, SystemMetrics>? _read;
    private readonly DiagnosticLog? _log;
    private readonly Timer _timer;
    private readonly object _gate = new();
    private WindowsSystemMetricsReader? _reader;
    private int _closed, _reading;
    private SystemMetrics _latest = new();
    private long _timestamp;
    private bool _reportedError;

    internal SystemMetricsService(SystemMetric enabled, DiagnosticLog? log = null, Func<SystemMetric, SystemMetrics>? read = null)
    {
        _enabled = enabled; _log = log; _read = read;
        _timer = new Timer(_ => Sample(), null, 0, 1000);
    }

    private void Sample()
    {
        if (Interlocked.CompareExchange(ref _reading, 1, 0) != 0) return;
        try
        {
            if (Volatile.Read(ref _closed) != 0) return;
            _reader ??= _read == null ? new WindowsSystemMetricsReader(_enabled) : null;
            var metrics = _read?.Invoke(_enabled) ?? _reader!.Read();
            lock (_gate) { if (_closed == 0) { _latest = metrics; _timestamp = Stopwatch.GetTimestamp(); } }
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        {
            if (!_reportedError) { _log?.Write("SystemMetrics", error); _reportedError = true; }
            lock (_gate) { _latest = new(); _timestamp = 0; }
        }
        finally
        {
            if (Volatile.Read(ref _closed) != 0) { _reader?.Dispose(); _reader = null; }
            Volatile.Write(ref _reading, 0);
        }
    }

    internal SystemMetrics Latest
    {
        get { lock (_gate) return _closed == 0 && _timestamp != 0 && Stopwatch.GetElapsedTime(_timestamp) <= TimeSpan.FromSeconds(3) ? _latest : new(); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0) return;
        _timer.Dispose();
        if (Interlocked.CompareExchange(ref _reading, 1, 0) == 0)
        { _reader?.Dispose(); _reader = null; Volatile.Write(ref _reading, 0); }
    }
}
