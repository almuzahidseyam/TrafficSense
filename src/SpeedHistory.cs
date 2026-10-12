namespace TrafficSense;

internal readonly record struct SpeedPoint(TimeSpan Timestamp, NetworkSpeed Speed);
internal sealed class SpeedHistory
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(2);
    public const int MaximumSamples = 256;
    private readonly Queue<SpeedPoint> _samples = new();
    public IReadOnlyCollection<SpeedPoint> Samples => _samples;
    public void Add(TimeSpan timestamp, NetworkSpeed speed)
    {
        if (_samples.TryPeek(out _) && timestamp <= _samples.Last().Timestamp) return;
        static double Clean(double value) => double.IsFinite(value) && value >= 0 ? value : 0;
        _samples.Enqueue(new(timestamp, new(Clean(speed.DownloadBytesPerSecond), Clean(speed.UploadBytesPerSecond))));
        while (_samples.Count > MaximumSamples || (_samples.TryPeek(out var first) && timestamp - first.Timestamp > Duration))
            _samples.Dequeue();
    }
}
