using System.Net.NetworkInformation;
using Xunit;

namespace TrafficSense.Tests;

public sealed class NetworkSamplingServiceTests
{
    private static NetworkAdapterInfo Adapter(long received, long sent) =>
        new("wifi", "Wi-Fi", "Wi-Fi", NetworkInterfaceType.Wireless80211, true, true, true, new("wifi", received, sent));

    [Fact]
    public void BusyUiDoesNotLoseSamplesAndBytesAreDeliveredOnlyOnceAcrossMidnight()
    {
        var now = new DateTimeOffset(2026, 10, 13, 23, 59, 59, TimeSpan.FromHours(6));
        TimeSpan time = TimeSpan.Zero;
        var adapter = Adapter(1000, 500);
        using var service = new NetworkSamplingService(() => [adapter], () => time, () => now, null, 250, start: false);
        service.Sample();
        for (int i = 1; i <= 40; i++)
        {
            time = TimeSpan.FromMilliseconds(i * 250);
            adapter = Adapter(1000 + i * 100, 500 + i * 50);
            service.Sample();
            if (i == 4) now = now.AddDays(1);
        }
        var result = service.Consume();
        Assert.Equal(new NetworkSpeed(400, 200), result.Speed);
        Assert.Equal(SampleQuality.Fresh, result.Quality);
        Assert.Collection(result.Usage,
            day => Assert.Equal(new UsageDay(new(2026, 10, 13), 400, 200), day),
            day => Assert.Equal(new UsageDay(new(2026, 10, 14), 3600, 1800), day));
        Assert.Empty(service.Consume().Usage);
    }

    [Fact]
    public async Task ResetDuringNativeReadDiscardsObsoleteSampleAndReadsNeverOverlap()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        int reads = 0;
        using var service = new NetworkSamplingService(() =>
        { Interlocked.Increment(ref reads); entered.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(5))); return [Adapter(5000, 1000)]; },
            () => TimeSpan.FromSeconds(1), () => DateTimeOffset.Now, null, 250, start: false);
        var task = Task.Run(service.Sample);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        service.Sample(); service.Reset();
        Assert.Equal(1, reads);
        // Consume must not wait for a blocked native call.
        Assert.Equal(SampleQuality.Sampling, service.Consume().Quality);
        release.Set(); await task;
        Assert.Null(service.Consume().Adapter);
        service.Sample();
        Assert.Equal(SampleQuality.Sampling, service.Consume().Quality);
    }

    [Fact]
    public void MissingReadCannotLeaveStaleSpeedAndExitPreservesPendingBytes()
    {
        TimeSpan time = TimeSpan.Zero; bool fail = false;
        var adapter = Adapter(1000, 500);
        var service = new NetworkSamplingService(() => fail ? throw new NetworkInformationException() : [adapter],
            () => time, () => DateTimeOffset.Now, null, 500, start: false);
        service.Sample(); time += TimeSpan.FromSeconds(1); adapter = Adapter(3000, 1000); service.Sample();
        fail = true; time += TimeSpan.FromSeconds(1); service.Sample();
        service.Dispose(); service.Sample(); service.Reset();
        var delivery = service.Consume();
        Assert.Equal(SampleQuality.Unavailable, delivery.Quality);
        Assert.Equal(default, delivery.Speed);
        Assert.Equal(2000, Assert.Single(delivery.Usage).Received);
        Assert.Empty(service.Consume().Usage);
    }
}
