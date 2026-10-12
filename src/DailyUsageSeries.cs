namespace TrafficSense;

internal sealed class DailyUsageSeries
{
    internal const int MaximumCalendarDays = 30;
    internal static DailyUsageSeries Empty { get; } = new([], default, default);
    internal IReadOnlyList<UsageDay> Days { get; }
    internal DateOnly Start { get; }
    internal DateOnly End { get; }
    internal int CalendarDays => Days.Count == 0 ? 0 : End.DayNumber - Start.DayNumber + 1;
    internal decimal PeakBytes => Days.Select(day => (decimal)Math.Max(day.Received, day.Sent)).DefaultIfEmpty().Max();

    internal DateOnly? DateAt(double position, double width)
    {
        if (Days.Count == 0 || !double.IsFinite(position) || !double.IsFinite(width) || width <= 0 || position < 0 || position >= width)
            return null;
        int offset = Math.Min(CalendarDays - 1, (int)(position / width * CalendarDays));
        return Start.AddDays(offset);
    }

    private DailyUsageSeries(UsageDay[] days, DateOnly start, DateOnly end)
    { Days = Array.AsReadOnly(days); Start = start; End = end; }

    internal static DailyUsageSeries Create(IEnumerable<UsageDay> selectedDays)
    {
        var ordered = selectedDays.OrderBy(day => day.Date).ToArray();
        if (ordered.Length == 0) return Empty;
        var end = ordered[^1].Date;
        int firstDay = Math.Max(ordered[0].Date.DayNumber, end.DayNumber - MaximumCalendarDays + 1);
        var start = DateOnly.FromDayNumber(firstDay);
        // Keep gaps in the calendar; absence of a record is not measured zero use.
        return new(ordered.Where(day => day.Date >= start).ToArray(), start, end);
    }
}
