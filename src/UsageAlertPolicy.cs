namespace TrafficSense;

internal readonly record struct UsageLimitAlert(bool Monthly, decimal UsedBytes, decimal LimitBytes);

internal sealed class UsageAlertPolicy
{
    private DateOnly? _date;
    private (int Year, int Month)? _month;
    private int _dailyLimit, _monthlyLimit;
    private decimal _previousDay, _previousMonth;

    internal IReadOnlyList<UsageLimitAlert> Observe(UsageLedger ledger, DateOnly date, int dailyGiB, int monthlyGiB)
    {
        decimal day = (decimal)ledger.Today(date).Received + ledger.Today(date).Sent;
        var monthRange = new UsageDateRange(new(date.Year, date.Month, 1),
            new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)));
        var total = ledger.GetTotals(monthRange).Total;
        var month = (date.Year, date.Month);
        var alerts = new List<UsageLimitAlert>(2);
        // Prime from stored totals on startup/reconfiguration. Restarting or
        // lowering a limit must not repeatedly notify for old traffic.
        bool initialized = _date.HasValue;
        decimal previousDay = _date == date ? _previousDay : 0;
        decimal previousMonth = _month == month ? _previousMonth : 0;
        decimal dailyBytes = (decimal)dailyGiB * 1024 * 1024 * 1024;
        decimal monthlyBytes = (decimal)monthlyGiB * 1024 * 1024 * 1024;
        if (initialized && dailyGiB == _dailyLimit && dailyGiB > 0 && previousDay < dailyBytes && day >= dailyBytes)
            alerts.Add(new(false, day, dailyBytes));
        if (initialized && monthlyGiB == _monthlyLimit && monthlyGiB > 0 && previousMonth < monthlyBytes && total >= monthlyBytes)
            alerts.Add(new(true, total, monthlyBytes));
        _date = date; _month = month; _previousDay = Math.Max(previousDay, day); _previousMonth = Math.Max(previousMonth, total);
        _dailyLimit = dailyGiB; _monthlyLimit = monthlyGiB;
        return alerts;
    }
}
