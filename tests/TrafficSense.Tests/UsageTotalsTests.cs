using System.Globalization;
using Xunit;

namespace TrafficSense.Tests;

public sealed class UsageTotalsTests
{
    [Fact]
    public void SelectedTotalsIncludeBothBoundariesAndOnlyRecordedDays()
    {
        var ledger = new UsageLedger([
            new(new(2024, 2, 28), 1000, 500), new(new(2024, 2, 29), 100, 20),
            new(new(2024, 3, 2), 200, 40), new(new(2024, 3, 3), 4000, 2000)]);
        Assert.Equal(new UsageTotals(300, 60, 2), ledger.GetTotals(new(new(2024, 2, 29), new(2024, 3, 2))));
        Assert.Equal(360, ledger.GetTotals(new(new(2024, 2, 29), new(2024, 3, 2))).Total);
        Assert.Equal(new UsageTotals(1100, 520, 2), ledger.GetTotals(UsageDateRange.ForMonth(new(2024, 2, 1))));
        Assert.Equal(new UsageTotals(200, 40, 1), ledger.GetTotals(new(new(2024, 3, 2), new(2024, 3, 2))));
        Assert.Equal(new UsageTotals(5300, 2560, 4), ledger.GetTotals());
    }

    [Fact]
    public void EmptySelectionIsZeroAndReversedRangeIsRejected()
    {
        var ledger = new UsageLedger([new(new(2026, 10, 12), 0, 0)]);
        Assert.Equal(new UsageTotals(0, 0, 1), ledger.GetTotals());
        Assert.Equal(default, ledger.GetTotals(UsageDateRange.ForMonth(new(2020, 1, 1))));
        Assert.Equal(default, new UsageLedger().GetTotals());
        Assert.Throws<ArgumentException>(() => ledger.GetTotals(new(new(2026, 10, 13), new(2026, 10, 12))));
    }

    [Fact]
    public void LiveTotalsDoNotMarkPendingTrafficSavedOrChangeStoredRecords()
    {
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.FromHours(6));
        var ledger = new UsageLedger([new(new(2026, 10, 12), 100, 50)]);
        _ = ledger.GetTotals();
        Assert.False(ledger.Dirty);
        ledger.Record(new(20, 10), now);
        Assert.Equal(new UsageTotals(120, 60, 1), ledger.GetTotals());
        Assert.True(ledger.Dirty);
        Assert.Equal(new UsageDay(new(2026, 10, 12), 120, 60), Assert.Single(ledger.Days));
    }

    [Fact]
    public void MaximumRetainedCountersSumExactlyAndAgreeWithCsv()
    {
        var ledger = new UsageLedger(Enumerable.Range(0, UsageLedger.MaximumDays)
            .Select(i => new UsageDay(new DateOnly(2024, 1, 1).AddDays(i), long.MaxValue, long.MaxValue)));
        var totals = ledger.GetTotals();
        Assert.Equal((decimal)long.MaxValue * 400, totals.Received);
        Assert.Equal((decimal)long.MaxValue * 400, totals.Sent);
        Assert.Equal((decimal)long.MaxValue * 800, totals.Total);
        Assert.Equal(400, totals.RecordedDays);
        string root = Path.Combine(Path.GetTempPath(), "TrafficSense.TotalsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "usage.csv");
        try
        {
            var range = new UsageDateRange(new(2024, 2, 29), new(2024, 3, 2));
            Assert.True(UsageCsv.Export(path, ledger, range).Success);
            var rows = File.ReadAllLines(path).Skip(1).Select(row => row.Split(',')).ToArray();
            var selected = ledger.GetTotals(range);
            Assert.Equal(selected.RecordedDays, rows.Length);
            Assert.Equal(selected.Received, rows.Sum(row => decimal.Parse(row[1], CultureInfo.InvariantCulture)));
            Assert.Equal(selected.Sent, rows.Sum(row => decimal.Parse(row[2], CultureInfo.InvariantCulture)));
            Assert.Equal(selected.Total, rows.Sum(row => decimal.Parse(row[3], CultureInfo.InvariantCulture)));
            Assert.False(ledger.Dirty);
        }
        finally { File.Delete(path); Directory.Delete(root); }
    }
}
