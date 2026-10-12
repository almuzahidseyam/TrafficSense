using System.Globalization;

namespace TrafficSense;

internal readonly record struct UsageDateRange(DateOnly Start, DateOnly End)
{
    internal bool IsValid => Start <= End;
    internal bool Contains(DateOnly date) => date >= Start && date <= End;
    internal static UsageDateRange ForMonth(DateOnly date) => new(
        new(date.Year, date.Month, 1), new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)));

    internal static bool TryParse(string? start, string? end, out UsageDateRange range)
    {
        range = default;
        if (!DateOnly.TryParseExact(start?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first) ||
            !DateOnly.TryParseExact(end?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last) || first > last)
            return false;
        range = new(first, last);
        return true;
    }
}
