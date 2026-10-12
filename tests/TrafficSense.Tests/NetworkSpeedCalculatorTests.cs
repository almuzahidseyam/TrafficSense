using Xunit;

namespace TrafficSense.Tests;

public class NetworkSpeedCalculatorTests
{
    [Fact]
    public void FirstSampleDoesNotCountLifetimeTraffic()
    {
        var calculator = new NetworkSpeedCalculator();
        var speed = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 8_000_000, 4_000_000) }, TimeSpan.Zero);

        Assert.Equal(default, speed);
    }

    [Fact]
    public void DelayedTickUsesActualElapsedTime()
    {
        var calculator = new NetworkSpeedCalculator();
        calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 1000, 500) }, TimeSpan.Zero);

        var speed = calculator.Update(new[] { new NetworkAdapterSnapshot("wifi", 5000, 1500) }, TimeSpan.FromSeconds(2));

        Assert.Equal(2000, speed.DownloadBytesPerSecond);
        Assert.Equal(500, speed.UploadBytesPerSecond);
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
