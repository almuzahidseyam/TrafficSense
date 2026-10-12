using System.Text.Json;
using Xunit;

namespace TrafficSense.Tests;

public sealed class UsageTests
{
    [Fact]
    public void GraphRetainsTwoMinutesAndRejectsNonFiniteRatesAndBackwardsTime()
    {
        var history = new SpeedHistory();
        for (int i = 0; i < 1000; i++) history.Add(TimeSpan.FromMilliseconds(i * 500), new(i, i * 2));
        Assert.InRange(history.Samples.Count, 1, SpeedHistory.MaximumSamples);
        Assert.Equal(TimeSpan.FromMinutes(2), history.Samples.Last().Timestamp - history.Samples.First().Timestamp);
        int count = history.Samples.Count;
        history.Add(TimeSpan.Zero, default);
        Assert.Equal(count, history.Samples.Count);
        history.Add(TimeSpan.FromSeconds(501), new(double.NaN, double.PositiveInfinity));
        Assert.Equal(default, history.Samples.Last().Speed);
        Assert.False(history.Samples.Last().IsValid);
    }

    [Fact]
    public void FastestIntervalRetainsTheEntireTwoMinuteGraphWindow()
    {
        var history = new SpeedHistory();
        for (int index = 0; index < 2000; index++) history.Add(TimeSpan.FromMilliseconds(index * 250), new(index, index));
        Assert.Equal(481, history.Samples.Count);
        Assert.Equal(SpeedHistory.Duration, history.Samples.Last().Timestamp - history.Samples.First().Timestamp);
    }

    [Fact]
    public void GraphDistinguishesUnknownIntervalsFromMeasuredIdle()
    {
        var history = new SpeedHistory();
        history.Add(TimeSpan.Zero, default, false);
        history.Add(TimeSpan.FromSeconds(1), default);
        history.Add(TimeSpan.FromSeconds(2), new(100, 50), false);
        Assert.Equal(new[] { false, true, false }, history.Samples.Select(point => point.IsValid));
        Assert.Equal(default, history.Samples.ElementAt(1).Speed);
    }

    [Fact]
    public void InvalidLedgerEntriesAreIgnoredAndDuplicateDaysCombine()
    {
        var date = new DateOnly(2026, 10, 12);
        var ledger = new UsageLedger([null!, new(date, -1, 0), new(date, 100, 20), new(date, 200, 40)]);
        Assert.Single(ledger.Days);
        Assert.Equal(300, ledger.Today(date).Received);
        Assert.Equal(60, ledger.Today(date).Sent);
    }

    [Fact]
    public void DailyAndMonthlyTotalsFollowLocalDateAndSurviveSerialization()
    {
        var ledger = new UsageLedger();
        ledger.Record(new(100, 20), new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.FromHours(6)));
        ledger.Record(new(200, 40), new DateTimeOffset(2026, 10, 1, 0, 0, 1, TimeSpan.FromHours(6)));
        ledger.Record(new(300, 60), new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(6)));
        var loaded = new UsageLedger(JsonSerializer.Deserialize<UsageDay[]>(JsonSerializer.Serialize(ledger.Days)));
        Assert.Equal(200, loaded.Today(new(2026, 10, 1)).Received);
        Assert.Equal(500, loaded.Month(new(2026, 10, 2)).Received);
        Assert.Equal(100, loaded.Month(new(2026, 10, 2)).Sent);
        Assert.Equal(100, loaded.Month(new(2026, 9, 30)).Received);
        Assert.Equal(0, loaded.Today(new(2027, 1, 1)).Received);
    }

    [Fact]
    public void CounterBaselinesResetsGapsAndAdapterChangesDoNotInflateUsage()
    {
        var calculator = new NetworkSpeedCalculator();
        var ledger = new UsageLedger();
        var now = DateTimeOffset.Now;
        void Sample(string id, long rx, long tx, double seconds)
        {
            calculator.Update([new(id, rx, tx)], TimeSpan.FromSeconds(seconds));
            ledger.Record(calculator.LastTraffic, now);
        }
        Sample("a", 10000, 20000, 0);
        Sample("a", 10100, 20050, 1);
        Sample("a", 100000, 200000, 20); // long sleep interval: baseline only
        Sample("b", 500000, 500000, 21); // changed adapter: baseline only
        Sample("b", 500200, 500100, 22);
        calculator.Reset();
        Sample("b", 600000, 600000, 23);
        Sample("b", 1, 1, 24); // counter reset
        Assert.Equal(300, ledger.Today(DateOnly.FromDateTime(now.Date)).Received);
        Assert.Equal(150, ledger.Today(DateOnly.FromDateTime(now.Date)).Sent);
    }

    [Fact]
    public void HistoryIsBoundedAndOverflowCannotMakeTotalsNegative()
    {
        var days = Enumerable.Range(0, 600).Select(i => new UsageDay(new DateOnly(2024, 1, 1).AddDays(i), 10, 20));
        var ledger = new UsageLedger(days);
        Assert.Equal(UsageLedger.MaximumDays, ledger.Days.Count);
        var now = DateTimeOffset.Now;
        ledger.Record(new(long.MaxValue, long.MaxValue), now);
        ledger.Record(new(1, 1), now);
        Assert.Equal(long.MaxValue, ledger.Today(DateOnly.FromDateTime(now.Date)).Received);
        Assert.Equal(long.MaxValue, ledger.Month(DateOnly.FromDateTime(now.Date)).Sent);
    }

    [Fact]
    public void StorePreservesExistingFileOnFailureAndRetriesDirtyData()
    {
        string root = Path.Combine(Path.GetTempPath(), "TrafficSense.UsageTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "usage.json");
        var store = new UsageStore(path);
        try
        {
            var ledger = store.Load();
            ledger.Record(new(100, 50), DateTimeOffset.Now);
            Assert.True(store.Save(ledger));
            Assert.False(ledger.Dirty);
            var saved = File.ReadAllBytes(path);
            ledger.Record(new(100, 50), DateTimeOffset.Now);
            using (var blocked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) Assert.False(store.Save(ledger));
            Assert.True(ledger.Dirty);
            Assert.Equal(saved, File.ReadAllBytes(path));
            Assert.True(store.Save(ledger));
            Assert.Equal(200, store.Load().Days.Single().Received);
            File.WriteAllText(path, "{broken");
            Assert.Empty(store.Load().Days);
        }
        finally
        {
            if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TrafficSense.UsageTests")) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
        }
    }
}
