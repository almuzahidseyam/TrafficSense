namespace TrafficSense;

internal readonly record struct NetworkAdapterSnapshot(string Id, long BytesReceived, long BytesSent)
{
    internal TimeSpan? CapturedAt { get; init; }
}
internal enum SampleQuality { Sampling, Fresh, Unavailable, Delayed }

internal readonly record struct NetworkSpeed(double DownloadBytesPerSecond, double UploadBytesPerSecond);
internal readonly record struct NetworkTraffic(long Received, long Sent);

internal sealed class NetworkSpeedCalculator
{
    // Allow ordinary delayed ticks, but do not present a sleep/stall interval
    // as current throughput. Power notifications also reset shorter sleeps.
    private static readonly TimeSpan MaximumSampleInterval = TimeSpan.FromSeconds(5);
    private Dictionary<string, NetworkAdapterSnapshot> _previous = new();
    private TimeSpan? _previousTimestamp;
    public NetworkTraffic LastTraffic { get; private set; }
    public SampleQuality Quality { get; private set; } = SampleQuality.Sampling;

    public void Reset()
    {
        _previous.Clear();
        _previousTimestamp = null;
        LastTraffic = default;
        Quality = SampleQuality.Sampling;
    }

    public NetworkSpeed Update(IEnumerable<NetworkAdapterSnapshot> snapshots, TimeSpan timestamp)
    {
        var current = snapshots
            .Where(sample => !string.IsNullOrWhiteSpace(sample.Id) && sample.BytesReceived >= 0 && sample.BytesSent >= 0)
            .GroupBy(sample => sample.Id, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        double received = 0;
        double sent = 0;
        long receivedBytes = 0, sentBytes = 0;
        double seconds = _previousTimestamp.HasValue
            ? (timestamp - _previousTimestamp.Value).TotalSeconds
            : 0;

        bool validInterval = seconds > 0 && seconds <= MaximumSampleInterval.TotalSeconds;
        Quality = current.Count == 0 ? SampleQuality.Unavailable : !validInterval && _previousTimestamp.HasValue
            ? SampleQuality.Delayed : SampleQuality.Sampling;
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

                double measuredSeconds = sample.CapturedAt.HasValue && previous.CapturedAt.HasValue
                    ? (sample.CapturedAt.Value - previous.CapturedAt.Value).TotalSeconds : seconds;
                if (measuredSeconds <= 0 || measuredSeconds > MaximumSampleInterval.TotalSeconds) continue;
                Quality = SampleQuality.Fresh;
                received += (sample.BytesReceived - previous.BytesReceived) / measuredSeconds;
                sent += (sample.BytesSent - previous.BytesSent) / measuredSeconds;
                receivedBytes = UsageLedger.Add(receivedBytes, sample.BytesReceived - previous.BytesReceived);
                sentBytes = UsageLedger.Add(sentBytes, sample.BytesSent - previous.BytesSent);
            }
        }

        _previous = current;
        _previousTimestamp = timestamp;
        LastTraffic = new(receivedBytes, sentBytes);

        return validInterval ? new NetworkSpeed(received, sent) : default;
    }
}
