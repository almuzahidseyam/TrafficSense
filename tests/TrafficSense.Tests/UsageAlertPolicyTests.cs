using Xunit;

namespace TrafficSense.Tests;

public sealed class UsageAlertPolicyTests
{
    private static readonly DateOnly Date = new(2026, 10, 13);
    private const long GiB = 1024 * 1024 * 1024;
    private static DateTimeOffset At(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(6));

    [Fact]
    public void ExactCombinedBoundaryNotifiesOnceAndStartupOverLimitDoesNotRepeat()
    {
        var ledger = new UsageLedger([new(Date, GiB - 2, 1)]); var policy = new UsageAlertPolicy();
        Assert.Empty(policy.Observe(ledger, Date, 1, 1));
        ledger.Record(new(0, 1), At(Date));
        var alerts = policy.Observe(ledger, Date, 1, 1);
        Assert.Equal(2, alerts.Count); Assert.All(alerts, alert => Assert.Equal(GiB, alert.UsedBytes));
        ledger.Record(new(GiB, 0), At(Date)); Assert.Empty(policy.Observe(ledger, Date, 1, 1));
        Assert.Empty(new UsageAlertPolicy().Observe(ledger, Date, 1, 1));
    }

    [Fact]
    public void LimitsReconfigureWithoutOldAlertsAndCalendarPeriodsRearmSeparately()
    {
        var ledger = new UsageLedger([new(Date, GiB, 0)]); var policy = new UsageAlertPolicy();
        policy.Observe(ledger, Date, 2, 2);
        Assert.Empty(policy.Observe(ledger, Date, 1, 2)); // Already over a lowered limit.
        var nextDay = Date.AddDays(1); ledger.Record(new(GiB, 0), At(nextDay));
        var alerts = policy.Observe(ledger, nextDay, 1, 2);
        Assert.Equal(2, alerts.Count);
        var nextMonth = new DateOnly(2026, 11, 1); ledger.Record(new(2 * GiB, 0), At(nextMonth));
        Assert.Equal(2, policy.Observe(ledger, nextMonth, 1, 2).Count);
    }

    [Fact]
    public void MaximumTotalsUseDecimalAndDisabledAlertsStayQuiet()
    {
        var ledger = new UsageLedger(); var policy = new UsageAlertPolicy();
        policy.Observe(ledger, Date, 10000, 10000);
        ledger.Record(new(long.MaxValue, long.MaxValue), At(Date));
        Assert.All(policy.Observe(ledger, Date, 10000, 10000), alert => Assert.Equal((decimal)long.MaxValue * 2, alert.UsedBytes));
        Assert.Empty(policy.Observe(ledger, Date, 0, 0));
    }
}
