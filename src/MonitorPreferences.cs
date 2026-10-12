namespace TrafficSense;

internal static class MonitorPreferences
{
    public static IReadOnlyList<int> Intervals { get; } = Array.AsReadOnly(new[] { 250, 500, 1000, 2000 });
    public static int NormalizeInterval(int value) => Intervals.Contains(value) ? value : 1000;
}
