namespace TrafficSense;

internal readonly record struct NetworkAdapterSnapshot(string Id, long BytesReceived, long BytesSent);

internal readonly record struct NetworkSpeed(double DownloadBytesPerSecond, double UploadBytesPerSecond);

internal sealed class NetworkSpeedCalculator
{
    // Allow ordinary delayed ticks, but do not present a sleep/stall interval
    // as current throughput. Power notifications also reset shorter sleeps.
    private static readonly TimeSpan MaximumSampleInterval = TimeSpan.FromSeconds(5);
    private Dictionary<string, NetworkAdapterSnapshot> _previous = new();
    private TimeSpan? _previousTimestamp;

    public void Reset()
    {
        _previous.Clear();
        _previousTimestamp = null;
    }

    public NetworkSpeed Update(IEnumerable<NetworkAdapterSnapshot> snapshots, TimeSpan timestamp)
    {
        var current = snapshots
            .Where(sample => sample.BytesReceived >= 0 && sample.BytesSent >= 0)
            .ToDictionary(sample => sample.Id);

        double received = 0;
        double sent = 0;
        double seconds = _previousTimestamp.HasValue
            ? (timestamp - _previousTimestamp.Value).TotalSeconds
            : 0;

        bool validInterval = seconds > 0 && seconds <= MaximumSampleInterval.TotalSeconds;
        if (validInterval)
        {
            foreach (var sample in current.Values)
            {
                // New/reconnected adapters need their own baseline. Their lifetime
                // byte counters must never be counted as traffic from this interval.
                if (!_previous.TryGetValue(sample.Id, out var previous) ||
                    sample.BytesReceived < previous.BytesReceived ||
                    sample.BytesSent < previous.BytesSent)
                {
                    continue;
                }

                received += sample.BytesReceived - previous.BytesReceived;
                sent += sample.BytesSent - previous.BytesSent;
            }
        }

        _previous = current;
        _previousTimestamp = timestamp;

        return validInterval ? new NetworkSpeed(received / seconds, sent / seconds) : default;
    }
}
