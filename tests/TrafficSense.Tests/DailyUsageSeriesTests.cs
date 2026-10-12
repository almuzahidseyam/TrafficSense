using Xunit;

namespace TrafficSense.Tests;

public sealed class DailyUsageSeriesTests
{
    [Fact]
    public void EmptySelectionHasNoDatesOrInventedRecords()
    {
        var chart = DailyUsageSeries.Create([]);
        Assert.Empty(chart.Days);
        Assert.Equal(0, chart.CalendarDays);
        Assert.Equal(0, chart.PeakBytes);
    }

    [Fact]
    public void LatestThirtyCalendarDaysIncludeBothBoundariesAndSortChronologically()
    {
        var start = new DateOnly(2026, 9, 1);
        var chart = DailyUsageSeries.Create(Enumerable.Range(0, 40).Reverse().Select(i => new UsageDay(start.AddDays(i), i, 2 * i)));
        Assert.Equal(start.AddDays(10), chart.Start);
        Assert.Equal(start.AddDays(39), chart.End);
        Assert.Equal(30, chart.CalendarDays);
        Assert.Equal(Enumerable.Range(10, 30).Select(i => start.AddDays(i)), chart.Days.Select(day => day.Date));
        Assert.Equal(78, chart.PeakBytes);
    }

    [Fact]
    public void GapsStayAbsentAndDoNotBecomeMeasuredZeros()
    {
        var chart = DailyUsageSeries.Create([new(new(2026, 9, 1), 0, 0), new(new(2026, 9, 30), 100, 50)]);
        Assert.Equal(30, chart.CalendarDays);
        Assert.Equal(2, chart.Days.Count);
        Assert.Equal(0, chart.Days[0].Received);
        Assert.DoesNotContain(chart.Days, day => day.Date == new DateOnly(2026, 9, 2));
    }

    [Fact]
    public void SparseOldRecordsDoNotExtendTheChartBeyondThirtyDays()
    {
        var chart = DailyUsageSeries.Create([new(new(2020, 1, 1), 9000, 9000), new(new(2026, 10, 12), 120, 60)]);
        Assert.Equal(new DateOnly(2026, 9, 13), chart.Start);
        Assert.Equal(new DateOnly(2026, 10, 12), chart.End);
        Assert.Single(chart.Days);
        Assert.Equal(120, chart.PeakBytes);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(9999, 12, 31)]
    public void SingleDayAtCalendarLimitsDoesNotOverflow(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        var chart = DailyUsageSeries.Create([new(date, 0, 0)]);
        Assert.Equal(date, chart.Start);
        Assert.Equal(date, chart.End);
        Assert.Equal(1, chart.CalendarDays);
        Assert.Single(chart.Days);
        Assert.Equal(0, chart.PeakBytes);
    }

    [Fact]
    public void PeakIsExactForMaximumCountersAndKeepsBothDirections()
    {
        var chart = DailyUsageSeries.Create([new(new(2026, 10, 12), long.MaxValue, long.MaxValue)]);
        Assert.Equal((decimal)long.MaxValue, chart.PeakBytes);
        Assert.Equal(long.MaxValue, chart.Days[0].Received);
        Assert.Equal(long.MaxValue, chart.Days[0].Sent);
    }

    [Fact]
    public void ChartSnapshotDoesNotChangeWhenNewTrafficReplacesTheSourceDay()
    {
        var days = new List<UsageDay> { new(new(2026, 10, 12), 100, 50) };
        var before = DailyUsageSeries.Create(days);
        days[0] = new(days[0].Date, 200, 80);
        var after = DailyUsageSeries.Create(days);
        Assert.Equal(100, before.PeakBytes);
        Assert.Equal(200, after.PeakBytes);
    }
}
