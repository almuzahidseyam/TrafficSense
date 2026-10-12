namespace TrafficSense;

[Flags]
internal enum SystemMetric { None = 0, Cpu = 1, Memory = 2, Frequency = 4, Gpu = 8, Disk = 16, All = 31 }

internal sealed record SystemMetrics(double? CpuPercent = null, ulong? TotalMemory = null, ulong? AvailableMemory = null,
    double? CpuMhz = null, double? GpuPercent = null, double? DiskPercent = null,
    double? DiskReadBytes = null, double? DiskWriteBytes = null)
{
    internal double? MemoryPercent => TotalMemory is > 0 && AvailableMemory.HasValue && AvailableMemory <= TotalMemory
        ? (TotalMemory.Value - AvailableMemory.Value) * 100d / TotalMemory.Value : null;
}

internal static class MetricMath
{
    internal static double? Valid(double? value) => value.HasValue && double.IsFinite(value.Value) && value >= 0 ? value : null;
    internal static double? Percent(double? value) => Valid(value) is { } valid ? Math.Clamp(valid, 0, 100) : null;

    // GPU Engine instances are per process. Sum processes sharing the same
    // physical engine, then report the busiest engine across all GPUs. Adding
    // 3D + copy + video engines together would invent utilization above 100%.
    internal static double? BusiestGpuEngine(IEnumerable<(string Instance, double Value)> values)
    {
        var engines = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in values)
        {
            int luid = name.IndexOf("luid_", StringComparison.OrdinalIgnoreCase);
            int engine = name.IndexOf("_eng_", StringComparison.OrdinalIgnoreCase);
            if (luid < 0 || engine < luid || Valid(value) == null) continue;
            string key = name[luid..];
            engines[key] = engines.GetValueOrDefault(key) + value;
        }
        return engines.Count == 0 ? null : Percent(engines.Values.Max());
    }
}
