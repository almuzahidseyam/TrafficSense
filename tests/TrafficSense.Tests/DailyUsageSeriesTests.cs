using Xunit;

namespace TrafficSense.Tests;

public sealed class DailyUsageSeriesTests
{
    [Theory]
    [InlineData(0, 300, 0)]
    [InlineData(9.99, 300, 0)]
    [InlineData(10, 300, 1)]
    [InlineData(299.99, 300, 29)]
    [InlineData(0, 600, 0)]
    [InlineData(20, 600, 1)]
    [InlineData(599.99, 600, 29)]
    public void HoverMapsCalendarSlotsIncludingMissingDaysAndResizing(double x, double width, int offset)
    {
        var start = new DateOnly(2026, 12, 20);
        var chart = DailyUsageSeries.Create([new(start, 100, 50), new(start.AddDays(29), 200, 75)]);
        Assert.Equal(start.AddDays(offset), chart.DateAt(x, width));
    }

    [Theory]
    [InlineData(-1, 300)]
    [InlineData(300, 300)]
    [InlineData(301, 300)]
    [InlineData(0, 0)]
    [InlineData(0, -1)]
    [InlineData(double.NaN, 300)]
    [InlineData(double.PositiveInfinity, 300)]
    [InlineData(0, double.NaN)]
    [InlineData(0, double.PositiveInfinity)]
    public void HoverOutsidePlotOrBeforeLayoutHasNoDate(double x, double width)
    {
        var chart = DailyUsageSeries.Create([new(new(2026, 10, 12), 100, 50)]);
        Assert.Null(chart.DateAt(x, width));
    }

    [Fact]
    public void EmptyChartHasNoHoverDate() => Assert.Null(DailyUsageSeries.Empty.DateAt(0, 300));

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(9999, 12, 31)]
    public void SingleDayHoverAtCalendarLimitsDoesNotOverflow(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        var chart = DailyUsageSeries.Create([new(date, 0, 0)]);
        Assert.Equal(date, chart.DateAt(299.999, 300));
    }

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
