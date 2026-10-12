using System.Globalization;
using Xunit;

namespace TrafficSense.Tests;

public sealed class UsageDateRangeTests
{
    [Theory]
    [InlineData("2026-10-01", "2026-10-31")]
    [InlineData("2024-02-29", "2024-02-29")]
    [InlineData("2025-12-31", "2026-01-01")]
    [InlineData("0001-01-01", "9999-12-31")]
    [InlineData(" 2026-10-01 ", "2026-10-31 ")]
    public void ParsesInclusiveIsoRanges(string first, string last)
    {
        Assert.True(UsageDateRange.TryParse(first, last, out var range));
        Assert.True(range.IsValid);
        Assert.True(range.Contains(range.Start)); Assert.True(range.Contains(range.End));
    }

    [Theory]
    [InlineData(null, "2026-10-01")]
    [InlineData("", "2026-10-01")]
    [InlineData("2026-10-01", "")]
    [InlineData("2026-10-31", "2026-10-01")]
    [InlineData("2026-02-29", "2026-03-01")]
    [InlineData("2026-04-31", "2026-05-01")]
    [InlineData("10/01/2026", "10/31/2026")]
    [InlineData("2026-1-1", "2026-01-31")]
    [InlineData("2026-10-01T00:00:00", "2026-10-31")]
    public void RejectsMissingInvalidAmbiguousOrReversedDates(string? first, string? last)
        => Assert.False(UsageDateRange.TryParse(first, last, out _));

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2026, 2, 28)]
    [InlineData(2026, 12, 31)]
    [InlineData(9999, 12, 31)]
    public void MonthIncludesOnlyItsOwnDaysAndHandlesLeapAndMaximumYear(int year, int month, int last)
    {
        var range = UsageDateRange.ForMonth(new(year, month, 15));
        Assert.Equal(new DateOnly(year, month, 1), range.Start);
        Assert.Equal(new DateOnly(year, month, last), range.End);
        Assert.False(range.Contains(range.Start.AddDays(-1)));
        if (range.End != DateOnly.MaxValue) Assert.False(range.Contains(range.End.AddDays(1)));
    }

    [Theory]
    [InlineData("bn-BD")]
    [InlineData("de-DE")]
    [InlineData("ar-SA")]
    public void ParsingIgnoresUiAndRegionalCalendar(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.True(UsageDateRange.TryParse("2026-10-01", "2026-10-31", out var range));
            Assert.Equal(new DateOnly(2026, 10, 1), range.Start);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
