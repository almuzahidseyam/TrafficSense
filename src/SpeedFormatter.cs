namespace TrafficSense;

internal static class SpeedFormatter
{
    public static string Format(double bytesPerSecond)
    {
        if (!double.IsFinite(bytesPerSecond) || bytesPerSecond < 0)
        {
            bytesPerSecond = 0;
        }

        if (bytesPerSecond >= 1024.0 * 1024 * 1024)
            return $"{bytesPerSecond / (1024.0 * 1024 * 1024):F2} GB/s";
        if (bytesPerSecond >= 1024.0 * 1024)
            return $"{bytesPerSecond / (1024.0 * 1024):F2} MB/s";
        if (bytesPerSecond >= 1024)
            return $"{bytesPerSecond / 1024:F2} KB/s";

        return $"{bytesPerSecond:F0} B/s";
    }
}
