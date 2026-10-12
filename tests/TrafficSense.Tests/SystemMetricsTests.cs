using Xunit;

namespace TrafficSense.Tests;

public sealed class SystemMetricsTests
{
    [Fact]
    public void GpuAggregatesProcessesPerPhysicalEngineAndSelectsBusiestWithoutAddingDifferentEngines()
    {
        Assert.Equal(75, MetricMath.BusiestGpuEngine([
            ("pid_1_luid_0x0_0x1_phys_0_eng_0_engtype_3D", 40),
            ("pid_2_luid_0x0_0x1_phys_0_eng_0_engtype_3D", 35),
            ("pid_1_luid_0x0_0x1_phys_0_eng_1_engtype_Copy", 60),
            ("pid_3_luid_0x0_0x2_phys_0_eng_0_engtype_3D", 50),
            ("unknown", 100), ("pid_1_luid_0x0_0x1_phys_0_eng_0_engtype_3D", double.NaN)]));
    }

    [Fact]
    public void UnsupportedCountersDifferFromMeasuredZeroAndInvalidValuesNeverLeak()
    {
        Assert.Null(MetricMath.BusiestGpuEngine([]));
        Assert.Equal(0, MetricMath.BusiestGpuEngine([("pid_1_luid_1_phys_0_eng_0_engtype_3D", 0)]));
        Assert.Null(MetricMath.Valid(double.PositiveInfinity)); Assert.Null(MetricMath.Valid(-1));
        Assert.Equal(100, MetricMath.Percent(101));
        Assert.Null(new SystemMetrics().MemoryPercent);
        Assert.Null(new SystemMetrics(TotalMemory: 10, AvailableMemory: 11).MemoryPercent);
        Assert.Equal(25, new SystemMetrics(TotalMemory: ulong.MaxValue, AvailableMemory: ulong.MaxValue / 4 * 3).MemoryPercent!.Value, 8);
    }
}
