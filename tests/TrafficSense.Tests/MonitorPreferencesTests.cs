using System.Globalization;
using Xunit;

namespace TrafficSense.Tests;

public class MonitorPreferencesTests
{
    [Theory]
    [InlineData(125, "1.00 Kbps")]
    [InlineData(125000, "1.00 Mbps")]
    [InlineData(125000000, "1.00 Gbps")]
    [InlineData(-1, "0 bps")]
    [InlineData(double.NaN, "0 bps")]
    [InlineData(double.PositiveInfinity, "0 bps")]
    public void BitRatesUseEightBitsPerByteAndDecimalUnits(double value, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; Assert.Equal(expected, SpeedFormatter.Format(value, true)); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(500, 500)]
    [InlineData(1000, 1000)]
    [InlineData(2000, 2000)]
    [InlineData(0, 1000)]
    [InlineData(10000, 1000)]
    public void IntervalStaysWithinSupportedSamplingWindow(int value, int expected) =>
        Assert.Equal(expected, MonitorPreferences.NormalizeInterval(value));
}
