namespace TrafficSense;

internal sealed record UsageDay(DateOnly Date, long Received, long Sent);

internal readonly record struct UsageTotals(decimal Received, decimal Sent, int RecordedDays)
{
    public decimal Total => Received + Sent;
}

internal sealed class UsageLedger
{
    public const int MaximumDays = 400;
    private readonly SortedDictionary<DateOnly, UsageDay> _days = new();
    public IReadOnlyCollection<UsageDay> Days => _days.Values;
    public bool Dirty { get; private set; }

    public UsageLedger(IEnumerable<UsageDay>? days = null)
    {
        foreach (var day in days ?? [])
        {
            if (day is null || day.Received < 0 || day.Sent < 0) continue;
            _days.TryGetValue(day.Date, out var previous);
            _days[day.Date] = new(day.Date, Add(previous?.Received ?? 0, day.Received), Add(previous?.Sent ?? 0, day.Sent));
        }
        Trim();
    }

    public void Record(NetworkTraffic traffic, DateTimeOffset now)
    {
        if (traffic.Received < 0 || traffic.Sent < 0 || traffic == default) return;
        var date = DateOnly.FromDateTime(now.Date);
        var previous = Today(date);
        _days[date] = new(date, Add(previous.Received, traffic.Received), Add(previous.Sent, traffic.Sent));
        Dirty = true;
        Trim();
    }

    public UsageDay Today(DateOnly date) => _days.TryGetValue(date, out var day) ? day : new(date, 0, 0);
    public UsageTotals GetTotals(UsageDateRange? range = null)
    {
        if (range is { IsValid: false }) throw new ArgumentException("Invalid date range.", nameof(range));
        decimal received = 0, sent = 0;
        int count = 0;
        foreach (var day in _days.Values)
        {
            if (range != null && !range.Value.Contains(day.Date)) continue;
            // Up to 400 Int64 day counters fit exactly in decimal, including their combined total.
            received += day.Received; sent += day.Sent; count++;
        }
        return new(received, sent, count);
    }
    public UsageDay Month(DateOnly date)
    {
        long received = 0, sent = 0;
        foreach (var day in _days.Values.Where(d => d.Date.Year == date.Year && d.Date.Month == date.Month))
        { received = Add(received, day.Received); sent = Add(sent, day.Sent); }
        return new(date, received, sent);
    }
    public void MarkSaved() => Dirty = false;
    public static long Add(long left, long right) => left > long.MaxValue - right ? long.MaxValue : left + right;
    private void Trim() { while (_days.Count > MaximumDays) _days.Remove(_days.Keys.First()); }
}
