using Xunit;

namespace TrafficSense.Tests;

public class NetworkSpeedCalculatorTests
{
    [Fact]
    public void AdapterReadTimeAvoidsEnumerationLatencyBias()
    {
        var calculator = new NetworkSpeedCalculator();
        calculator.Update([new("wifi", 1000, 500) { CapturedAt = TimeSpan.Zero }], TimeSpan.FromSeconds(1));
        var speed = calculator.Update([new("wifi", 3000, 1000) { CapturedAt = TimeSpan.FromSeconds(1) }], TimeSpan.FromSeconds(3));
        Assert.Equal(new NetworkSpeed(2000, 500), speed);
        Assert.Equal(new NetworkTraffic(2000, 500), calculator.LastTraffic);
    }

    [Fact]
    public void InvalidAndDuplicateInterfacesAreRejectedWithoutCrashOrDoubleCounting()
    {
        var calculator = new NetworkSpeedCalculator();
        calculator.Update([new("wifi", 1000, 500)], TimeSpan.Zero);
        var speed = calculator.Update([new("wifi", 3000, 1000), new("wifi", 3000, 1000), new("", 10, 10), new("bad", -1, 10)], TimeSpan.FromSeconds(1));
        Assert.Equal(default, speed);
        Assert.Equal(SampleQuality.Unavailable, calculator.Quality);
        Assert.Equal(default, calculator.LastTraffic);
    }
    [Theory]
    [InlineData(6)]
    [InlineData(600)]
    public void LongSamplingGapStartsFreshInterval(double gapSeconds)
    {
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 1000, 500) }, TimeSpan.Zero);
        var resumed = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 9_000_000, 6_000_000) }, TimeSpan.FromSeconds(gapSeconds));
        var next = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 9_002_000, 6_000_500) }, TimeSpan.FromSeconds(gapSeconds + 1));

        Assert.Equal(default, resumed);
        Assert.Equal(new NetworkSpeed(2000, 500), next);
    }

    [Fact]
    public void FirstSampleDoesNotCountLifetimeTraffic()
    {
        var calculator = new NetworkSpeedCalculator();
        var speed = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 8_000_000, 4_000_000) }, TimeSpan.Zero);

        Assert.Equal(default, speed);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void DelayedTickUsesActualElapsedTime(double seconds)
    {
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 1000, 500) }, TimeSpan.Zero);

        var speed = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 5000, 1500) }, TimeSpan.FromSeconds(seconds));

        Assert.Equal(4000 / seconds, speed.DownloadBytesPerSecond);
        Assert.Equal(1000 / seconds, speed.UploadBytesPerSecond);
    }

    [Fact]
    public void ResetBetweenTicksDoesNotCountFastReconnectTraffic()
    {
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 1000, 500) }, TimeSpan.Zero);
        calculator.Reset();
        calculator.Reset(); // Address/availability/resume notifications can overlap.
        var reconnected = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 9_000_000, 6_000_000) }, TimeSpan.FromSeconds(1));
        var next = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 9_002_000, 6_000_500) }, TimeSpan.FromSeconds(2));

        Assert.Equal(default, reconnected);
        Assert.Equal(new NetworkSpeed(2000, 500), next);
    }

    [Fact]
    public void BackwardClockStartsFreshInterval()
    {
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 1000, 500) }, TimeSpan.FromSeconds(10));
        var reset = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 9000, 6000) }, TimeSpan.FromSeconds(5));
        var next = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 11_000, 6500) }, TimeSpan.FromSeconds(6));

        Assert.Equal(default, reset);
        Assert.Equal(new NetworkSpeed(2000, 500), next);
    }

    [Fact]
    public void SwitchingAdaptersDoesNotCreateSpeedSpike()
    {
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 1000, 500) }, TimeSpan.Zero);
        var switched = calculator.Update(new[] { new NetworkAdapterSnapshot("ethernet", 9_000_000, 6_000_000) }, TimeSpan.FromSeconds(1));
        var next = calculator.Update(new[] { new NetworkAdapterSnapshot("ethernet", 9_002_000, 6_000_500) }, TimeSpan.FromSeconds(2));

        Assert.Equal(default, switched);
        Assert.Equal(2000, next.DownloadBytesPerSecond);
        Assert.Equal(500, next.UploadBytesPerSecond);
    }

    [Fact]
    public void RemovingAdapterDoesNotCancelTrafficOnRemainingAdapter()
    {
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(new[]
        {
            new NetworkAdapterSnapshot("wifi", 1000, 500),
            new NetworkAdapterSnapshot("ethernet", 9_000_000, 6_000_000)
        }, TimeSpan.Zero);

        var speed = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 3000, 1000) }, TimeSpan.FromSeconds(1));

        Assert.Equal(2000, speed.DownloadBytesPerSecond);
        Assert.Equal(500, speed.UploadBytesPerSecond);
    }

    [Fact]
    public void AddingAdapterKeepsExistingAdaptersTraffic()
    {
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 1000, 500) }, TimeSpan.Zero);

        var speed = calculator.Update(new[]
        {
            new NetworkAdapterSnapshot("wifi", 3000, 1000),
            new NetworkAdapterSnapshot("ethernet", 9_000_000, 6_000_000)
        }, TimeSpan.FromSeconds(1));

        Assert.Equal(2000, speed.DownloadBytesPerSecond);
        Assert.Equal(500, speed.UploadBytesPerSecond);
    }

    [Fact]
    public void CounterResetCreatesNewBaseline()
    {
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 9000, 6000) }, TimeSpan.Zero);
        var reset = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 100, 50) }, TimeSpan.FromSeconds(1));
        var next = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 2100, 550) }, TimeSpan.FromSeconds(2));

        Assert.Equal(default, reset);
        Assert.Equal(2000, next.DownloadBytesPerSecond);
        Assert.Equal(500, next.UploadBytesPerSecond);
    }

    [Fact]
    public void MissingSampleDoesNotCountTrafficFromDisconnectedInterval()
    {
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 1000, 500) }, TimeSpan.Zero);
        var disconnected = calculator.Update(Array.Empty<NetworkAdapterSnapshot>(), TimeSpan.FromSeconds(1));
        var reconnected = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 100_000, 50_000) }, TimeSpan.FromSeconds(5));

        Assert.Equal(default, disconnected);
        Assert.Equal(default, reconnected);
    }

    [Fact]
    public void ConcurrentAdaptersAreSummedAfterEachHasBaseline()
    {
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(new[]
        {
            new NetworkAdapterSnapshot("wifi", 1000, 500),
            new NetworkAdapterSnapshot("ethernet", 2000, 1000)
        }, TimeSpan.Zero);
        var speed = calculator.Update(new[]
        {
            new NetworkAdapterSnapshot("wifi", 3000, 1500),
            new NetworkAdapterSnapshot("ethernet", 6000, 3000)
        }, TimeSpan.FromSeconds(2));

        Assert.Equal(3000, speed.DownloadBytesPerSecond);
        Assert.Equal(1500, speed.UploadBytesPerSecond);
    }

    [Fact]
    public void DuplicateTimestampDoesNotDivideByZero()
    {
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 1000, 500) }, TimeSpan.Zero);

        var speed = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 3000, 1000) }, TimeSpan.Zero);

        Assert.Equal(default, speed);
    }
}
