using Xunit;

namespace TrafficSense.Tests;

public sealed class MeasurementTraceTests
{
    [Theory]
    [InlineData(250)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(2000)]
    public void JitteredReadTraceConservesExactBytesWithoutRateBias(int nominalMilliseconds)
    {
        const long downloadRate = 125_000_000, uploadRate = 12_500_000;
        // Beyond double's exact integer range: subtract lifetime counters before converting rates.
        const long baseline = 9_007_199_254_740_999;
        long received = baseline, sent = baseline / 2, totalReceived = 0, totalSent = 0, ticks = 0;
        var calculator = new NetworkSpeedCalculator();
        calculator.Update([new("nic", received, sent) { CapturedAt = TimeSpan.Zero }], TimeSpan.Zero);
        for (int i = 0; i < 4096; i++)
        {
            int milliseconds = nominalMilliseconds + i % 71 - 35;
            ticks += milliseconds * TimeSpan.TicksPerMillisecond;
            long deltaReceived = downloadRate * milliseconds / 1000, deltaSent = uploadRate * milliseconds / 1000;
            received += deltaReceived; sent += deltaSent;
            // Enumeration adds variable latency after the actual native counter read.
            var captured = TimeSpan.FromTicks(ticks);
            var speed = calculator.Update([new("nic", received, sent) { CapturedAt = captured }], captured + TimeSpan.FromMilliseconds(i % 97));
            Assert.InRange(Math.Abs(speed.DownloadBytesPerSecond / downloadRate - 1), 0, 1e-12);
            Assert.InRange(Math.Abs(speed.UploadBytesPerSecond / uploadRate - 1), 0, 1e-12);
            Assert.Equal(SampleQuality.Fresh, calculator.Quality);
            totalReceived += calculator.LastTraffic.Received; totalSent += calculator.LastTraffic.Sent;
        }
        Assert.Equal(received - baseline, totalReceived);
        Assert.Equal(sent - baseline / 2, totalSent);
        Assert.True(totalReceived > uint.MaxValue);
    }

    [Fact]
    public void SingleByteDeltasSurviveLargeLifetimeCounters()
    {
        var calculator = new NetworkSpeedCalculator(); long start = long.MaxValue - 1000;
        calculator.Update([new("nic", start, start)], TimeSpan.Zero);
        Assert.Equal(new NetworkSpeed(4, 4), calculator.Update([new("nic", start + 1, start + 1)], TimeSpan.FromMilliseconds(250)));
        Assert.Equal(new NetworkTraffic(1, 1), calculator.LastTraffic);
    }
}
