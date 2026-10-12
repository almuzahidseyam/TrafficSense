using System.ComponentModel;
using System.Net.NetworkInformation;

namespace TrafficSense;

internal sealed record SamplingDelivery(NetworkSpeed Speed, NetworkAdapterInfo? Adapter, SampleQuality Quality,
    TimeSpan Timestamp, IReadOnlyList<UsageDay> Usage);

// No WPF dependency: counters keep advancing while the dispatcher is busy.
// Only one read can run at a time. Delivery drains exact bytes once and retains
// one latest speed, rather than queueing a UI callback per timer tick.
internal sealed class NetworkSamplingService : IDisposable
{
    private readonly object _gate = new();
    private readonly Func<IReadOnlyCollection<NetworkAdapterInfo>> _read;
    private readonly Func<TimeSpan> _clock;
    private readonly Func<DateTimeOffset> _localTime;
    private readonly DiagnosticLog? _log;
    private readonly NetworkSpeedCalculator _calculator = new();
    private readonly NetworkAdapterSelection _selection = new();
    private UsageLedger _pending = new();
    private readonly Timer _timer;
    private readonly bool _running;
    private string? _selectedId;
    private int _reading, _generation;
    private bool _closed;
    private TimeSpan _lastError = TimeSpan.MinValue;
    private SamplingDelivery _latest = new(default, null, SampleQuality.Sampling, TimeSpan.Zero, []);
    internal TimeSpan LatestTimestamp { get { lock (_gate) return _latest.Timestamp; } }

    internal NetworkSamplingService(Func<IReadOnlyCollection<NetworkAdapterInfo>> read, Func<TimeSpan> clock,
        Func<DateTimeOffset> localTime, string? selectedId, int interval, DiagnosticLog? log = null, bool start = true)
    {
        _read = read; _clock = clock; _localTime = localTime; _selectedId = selectedId; _log = log; _running = start;
        _timer = new Timer(_ => Sample(), null, start ? 0 : Timeout.Infinite,
            start ? MonitorPreferences.NormalizeInterval(interval) : Timeout.Infinite);
    }

    internal void Configure(string? selectedId, int interval, bool reset = false)
    {
        lock (_gate)
        {
            if (_closed) return;
            if (reset || selectedId != _selectedId)
            {
                _generation++; _selectedId = selectedId; _calculator.Reset();
                _latest = _latest with { Speed = default, Adapter = null, Quality = SampleQuality.Sampling };
            }
            if (_running) _timer.Change(0, MonitorPreferences.NormalizeInterval(interval));
        }
    }

    internal void Reset()
    {
        lock (_gate)
        {
            if (_closed) return;
            _generation++; _calculator.Reset();
            _latest = _latest with { Speed = default, Adapter = null, Quality = SampleQuality.Sampling };
        }
    }

    internal void Sample()
    {
        if (Interlocked.Exchange(ref _reading, 1) != 0) return;
        try
        {
            int generation;
            lock (_gate) { if (_closed) return; generation = _generation; }
            IReadOnlyCollection<NetworkAdapterInfo> adapters;
            try { adapters = _read(); }
            catch (Exception error) when (error is NetworkInformationException or Win32Exception or InvalidOperationException)
            {
                adapters = [];
                var now = _clock();
                if (_lastError == TimeSpan.MinValue || now - _lastError >= TimeSpan.FromMinutes(1))
                { _log?.Write("NetworkSample", error); _lastError = now; }
            }
            var timestamp = _clock();
            var localTime = _localTime();
            lock (_gate)
            {
                // A reconnect/configuration change during a native read makes
                // that read obsolete. Exit never publishes late samples.
                if (_closed || generation != _generation) return;
                var adapter = _selection.Select(adapters, _selectedId);
                NetworkAdapterSnapshot[] snapshots = adapter?.Snapshot is { } sample ? [sample] : [];
                var speed = _calculator.Update(snapshots, timestamp);
                _pending.Record(_calculator.LastTraffic, localTime);
                _latest = new(speed, adapter, _calculator.Quality, timestamp, []);
            }
        }
        finally { Volatile.Write(ref _reading, 0); }
    }

    internal SamplingDelivery Consume()
    {
        lock (_gate)
        {
            var delivery = _latest with { Usage = _pending.Days.ToArray() };
            // A new ledger avoids repeated delivery while retaining at most the
            // same 400 local calendar days as the persistent usage store.
            _pending = new();
            return delivery;
        }
    }

    public void Dispose()
    {
        lock (_gate) { if (_closed) return; _closed = true; _generation++; }
        _timer.Dispose();
    }
}
