using Xunit;

namespace TrafficSense.Tests;

public class SpeedFormatterTests
{
    [Theory]
    [InlineData(0, "B/s")]
    [InlineData(1023, "B/s")]
    [InlineData(1024, "KB/s")]
    [InlineData(1048576, "MB/s")]
    [InlineData(1073741824, "GB/s")]
    public void ExactUnitThresholdsUseNextUnit(double bytesPerSecond, string expectedUnit)
    {
        Assert.EndsWith(" " + expectedUnit, SpeedFormatter.Format(bytesPerSecond));
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidRateDisplaysZero(double bytesPerSecond)
    {
        Assert.Equal("0 B/s", SpeedFormatter.Format(bytesPerSecond));
    }
}
