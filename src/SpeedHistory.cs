namespace TrafficSense;

internal readonly record struct SpeedPoint(TimeSpan Timestamp, NetworkSpeed Speed, bool IsValid = true);
internal sealed class SpeedHistory
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(2);
    // Two minutes at the fastest supported 250 ms interval needs 481 points.
    public const int MaximumSamples = 512;
    private readonly Queue<SpeedPoint> _samples = new();
    public IReadOnlyCollection<SpeedPoint> Samples => _samples;
    public void Add(TimeSpan timestamp, NetworkSpeed speed, bool isValid = true)
    {
        if (_samples.TryPeek(out _) && timestamp <= _samples.Last().Timestamp) return;
        static double Clean(double value) => double.IsFinite(value) && value >= 0 ? value : 0;
        isValid &= double.IsFinite(speed.DownloadBytesPerSecond) && speed.DownloadBytesPerSecond >= 0 &&
            double.IsFinite(speed.UploadBytesPerSecond) && speed.UploadBytesPerSecond >= 0;
        _samples.Enqueue(new(timestamp, new(Clean(speed.DownloadBytesPerSecond), Clean(speed.UploadBytesPerSecond)), isValid));
        while (_samples.Count > MaximumSamples || (_samples.TryPeek(out var first) && timestamp - first.Timestamp > Duration))
            _samples.Dequeue();
    }
}
