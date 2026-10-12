using System.Globalization;
using System.Text;
using Xunit;

namespace TrafficSense.Tests;

public sealed class UsageCsvTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TrafficSense.CsvTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_root, "usage.csv");
    public UsageCsvTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData("en-US")]
    [InlineData("bn-BD")]
    [InlineData("de-DE")]
    [InlineData("ar-SA")]
    public void CsvUsesSortedIsoDatesExactBytesAndInvariantColumns(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var ledger = new UsageLedger([new(new(2026, 10, 12), 1024, 512), new(new(2026, 9, 30), 125, 0)]);
            Assert.True(UsageCsv.Export(FilePath, ledger).Success);
            Assert.Equal("Date,DownloadBytes,UploadBytes,TotalBytes\r\n2026-09-30,125,0,125\r\n2026-10-12,1024,512,1536\r\n",
                File.ReadAllText(FilePath));
            Assert.True(File.ReadAllBytes(FilePath).AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
            Assert.False(ledger.Dirty);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void TotalPreservesExactSumBeyondInt64WithoutOverflow()
    {
        var ledger = new UsageLedger([new(new(2026, 10, 12), long.MaxValue, long.MaxValue)]);
        Assert.True(UsageCsv.Export(FilePath, ledger).Success);
        Assert.Equal("2026-10-12,9223372036854775807,9223372036854775807,18446744073709551614", File.ReadAllLines(FilePath)[1]);
    }

    [Fact]
    public void ExportIncludesUnsavedTrafficWithoutClearingDirtyState()
    {
        var ledger = new UsageLedger();
        ledger.Record(new(200, 100), new(2026, 10, 12, 0, 15, 0, TimeSpan.FromHours(6)));
        Assert.True(UsageCsv.Export(FilePath, ledger).Success);
        Assert.Equal("2026-10-12,200,100,300", File.ReadAllLines(FilePath)[1]);
        Assert.True(ledger.Dirty);
        Assert.Equal(new UsageDay(new(2026, 10, 12), 200, 100), Assert.Single(ledger.Days));
    }

    [Fact]
    public void ExportsEveryRetainedDayWithoutInventingMissingDates()
    {
        var date = new DateOnly(2025, 1, 1);
        var ledger = new UsageLedger(Enumerable.Range(0, 500).Select(i => new UsageDay(date.AddDays(i * 2), i, i)));
        Assert.True(UsageCsv.Export(FilePath, ledger).Success);
        var rows = File.ReadAllLines(FilePath);
        Assert.Equal(401, rows.Length);
        Assert.Equal("2025-07-20,100,100,200", rows[1]);
        Assert.EndsWith(",499,499,998", rows[^1]);
    }

    [Fact]
    public void EmptyLedgerProducesHeaderOnlyCsv()
    {
        Assert.True(UsageCsv.Export(FilePath, new()).Success);
        Assert.Equal(UsageCsv.Header + "\r\n", File.ReadAllText(FilePath));
    }

    [Fact]
    public void ReplacementDoesNotAppendAndLeavesNoTemporaryFiles()
    {
        File.WriteAllText(FilePath, "old contents that must not remain");
        Assert.True(UsageCsv.Export(FilePath, new()).Success);
        Assert.Equal(UsageCsv.Header + "\r\n", File.ReadAllText(FilePath));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void FailedReplacementPreservesPreviousFileAndUsageAndCleansTemporaryFiles()
    {
        File.WriteAllText(FilePath, "existing file");
        var ledger = new UsageLedger();
        ledger.Record(new(1, 2), DateTimeOffset.Now);
        using (var locked = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.False(UsageCsv.Export(FilePath, ledger).Success);
        Assert.Equal("existing file", File.ReadAllText(FilePath));
        Assert.True(ledger.Dirty);
        Assert.Equal(1, Assert.Single(ledger.Days).Received);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void MissingParentAndDirectoryDestinationReturnFailure()
    {
        Assert.False(UsageCsv.Export(Path.Combine(_root, "missing", "usage.csv"), new()).Success);
        string directory = Path.Combine(_root, "directory.csv"); Directory.CreateDirectory(directory);
        Assert.False(UsageCsv.Export(directory, new()).Success);
        Assert.True(Directory.Exists(directory));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void RangeExportsOnlyInclusiveRecordedDatesWithoutChangingLedger()
    {
        var ledger = new UsageLedger([
            new(new(2025, 12, 30), 1, 1), new(new(2025, 12, 31), 2, 2),
            new(new(2026, 1, 1), 3, 3), new(new(2026, 1, 2), 4, 4)]);
        var range = new UsageDateRange(new(2025, 12, 31), new(2026, 1, 1));
        Assert.True(UsageCsv.Export(FilePath, ledger, range).Success);
        Assert.Equal(new[] { UsageCsv.Header, "2025-12-31,2,2,4", "2026-01-01,3,3,6" }, File.ReadAllLines(FilePath));
        Assert.Equal(4, ledger.Days.Count);
        Assert.False(ledger.Dirty);
    }

    [Fact]
    public void MonthExportsLeapDayAndOmitsOtherMonthsAndYears()
    {
        var ledger = new UsageLedger([
            new(new(2023, 2, 28), 1, 1), new(new(2024, 1, 31), 2, 2),
            new(new(2024, 2, 1), 3, 3), new(new(2024, 2, 29), 4, 4), new(new(2024, 3, 1), 5, 5)]);
        Assert.True(UsageCsv.Export(FilePath, ledger, UsageDateRange.ForMonth(new(2024, 2, 15))).Success);
        Assert.Equal(new[] { UsageCsv.Header, "2024-02-01,3,3,6", "2024-02-29,4,4,8" }, File.ReadAllLines(FilePath));
    }

    [Fact]
    public void InvalidRangeCannotReplaceExistingFileOrCreateTemporaryFiles()
    {
        File.WriteAllText(FilePath, "previous export");
        Assert.False(UsageCsv.Export(FilePath, new(), new(new(2026, 10, 31), new(2026, 10, 1))).Success);
        Assert.Equal("previous export", File.ReadAllText(FilePath));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void NoMatchingRowsProducesHeaderOnlyInWriter()
    {
        var ledger = new UsageLedger([new(new(2026, 10, 12), 10, 20)]);
        Assert.True(UsageCsv.Export(FilePath, ledger, new(new(2025, 1, 1), new(2025, 12, 31))).Success);
        Assert.Equal(UsageCsv.Header + "\r\n", File.ReadAllText(FilePath));
        Assert.Single(ledger.Days);
    }

    public void Dispose() => Directory.Delete(_root, true);
}
