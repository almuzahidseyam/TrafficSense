using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using Xunit;

namespace TrafficSense.Tests;

[SupportedOSPlatform("windows")]
public sealed class SingleInstanceGateTests
{
    private static string UniqueName() => @"Local\TrafficSense.Tests." + Guid.NewGuid().ToString("N");

    [Fact]
    public void SecondaryLaunchSignalsPrimaryWithoutTakingOwnership()
    {
        string name = UniqueName();
        using var activated = new ManualResetEventSlim();
        using var primary = new SingleInstanceGate(name);
        Assert.True(primary.IsPrimary);
        primary.Listen(activated.Set);

        OnAnotherThread(() =>
        {
            using var secondary = new SingleInstanceGate(name);
            Assert.False(secondary.IsPrimary);
            secondary.NotifyPrimary();
        });

        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void ActivationRequestedBeforeUiIsReadyIsRetained()
    {
        string name = UniqueName();
        using var activated = new ManualResetEventSlim();
        using var primary = new SingleInstanceGate(name);
        OnAnotherThread(() =>
        {
            using var secondary = new SingleInstanceGate(name);
            secondary.NotifyPrimary();
        });
        primary.Listen(activated.Set);
        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void OwnershipCanBeReacquiredAfterExitWhileAnotherHandleRemains()
    {
        string name = UniqueName();
        using var existingHandle = new Mutex(false, name + ".Mutex");
        using (var primary = new SingleInstanceGate(name)) Assert.True(primary.IsPrimary);
        using var restarted = new SingleInstanceGate(name);
        Assert.True(restarted.IsPrimary);
    }

    [Fact]
    public void AbandonedMutexAfterCrashCanBeRecovered()
    {
        string name = UniqueName();
        using var abandoned = new Mutex(false, name + ".Mutex");
        OnAnotherThread(() => Assert.True(abandoned.WaitOne(0))); // Thread exits owning it.
        // Thread.Join can observe the CLR's dead-thread state before native
        // teardown has abandoned the Windows mutex. Wait for observable recovery
        // without changing the production gate's nonblocking ownership check.
        Assert.True(SpinWait.SpinUntil(() =>
        {
            using var recovered = new SingleInstanceGate(name);
            return recovered.IsPrimary;
        }, TimeSpan.FromSeconds(5)), "The abandoned instance gate did not recover.");
    }

    [Fact]
    public void SimultaneousLaunchesElectExactlyOnePrimary()
    {
        string name = UniqueName();
        using var ready = new CountdownEvent(8);
        using var release = new ManualResetEventSlim();
        using var start = new Barrier(8);
        var errors = new ConcurrentQueue<Exception>();
        int owners = 0;
        var threads = Enumerable.Range(0, 8).Select(_ => new Thread(() =>
        {
            try
            {
                start.SignalAndWait(TimeSpan.FromSeconds(10));
                using var gate = new SingleInstanceGate(name);
                if (gate.IsPrimary) Interlocked.Increment(ref owners);
                ready.Signal();
                if (!release.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Test did not release the instance.");
            }
            catch (Exception error) { errors.Enqueue(error); }
        }) { IsBackground = true }).ToArray();
        foreach (var thread in threads) thread.Start();
        bool allReady;
        try { allReady = ready.Wait(TimeSpan.FromSeconds(5)); }
        finally { release.Set(); }
        foreach (var thread in threads) Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        Assert.Empty(errors);
        Assert.True(allReady);
        Assert.Equal(1, owners);
    }

    [Fact]
    public void IndependentSessionNamesDoNotBlockOneAnother()
    {
        using var first = new SingleInstanceGate(UniqueName());
        using var second = new SingleInstanceGate(UniqueName());
        Assert.True(first.IsPrimary);
        Assert.True(second.IsPrimary);
    }

    [Fact]
    public void DisposalIsIdempotentAndStopsListening()
    {
        var primary = new SingleInstanceGate(UniqueName());
        primary.Listen(() => { });
        primary.Dispose();
        primary.Dispose();
        Assert.Throws<ObjectDisposedException>(() => primary.NotifyPrimary());
    }

    private static void OnAnotherThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
        }) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
