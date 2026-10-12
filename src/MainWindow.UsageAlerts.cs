using System.Globalization;

namespace TrafficSense;

internal sealed record UsageAlertNotification(string Title, string Message);

public partial class MainWindow
{
    private readonly UsageAlertPolicy _usageAlerts = new();
    private readonly Action<UsageAlertNotification>? _usageNotificationSink;
    internal event Action<UsageAlertNotification>? UsageAlert;

    internal bool ChangeUsageLimits(int dailyGiB, int monthlyGiB)
    {
        var previous = (_settings.DailyLimitGiB, _settings.MonthlyLimitGiB);
        (_settings.DailyLimitGiB, _settings.MonthlyLimitGiB) = (Math.Clamp(dailyGiB, 0, 10000), Math.Clamp(monthlyGiB, 0, 10000));
        var result = _saveSettings(_settings);
        if (!result.Success) { (_settings.DailyLimitGiB, _settings.MonthlyLimitGiB) = previous; ShowSettingsError(result.Error); }
        CheckUsageLimits(); ApplyCustomizationLanguage(); return result.Success;
    }
    private void CheckUsageLimits()
    {
        if (_settings.DailyLimitGiB == 0 && _settings.MonthlyLimitGiB == 0) return;
        foreach (var alert in _usageAlerts.Observe(_usage, DateOnly.FromDateTime(_localTime().Date), _settings.DailyLimitGiB, _settings.MonthlyLimitGiB))
        {
            string title = Localization[alert.Monthly ? TextKey.MonthlyLimit : TextKey.DailyLimit];
            string message = string.Format(CultureInfo.CurrentCulture, Localization[TextKey.LimitReached],
                (alert.UsedBytes / (1024m * 1024 * 1024)).ToString("F2", CultureInfo.CurrentCulture),
                (alert.LimitBytes / (1024m * 1024 * 1024)).ToString("0", CultureInfo.CurrentCulture));
            var notification = new UsageAlertNotification(title, message);
            try
            {
                if (_usageNotificationSink != null) _usageNotificationSink(notification); else UsageAlert?.Invoke(notification);
            }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
            { _diagnostics?.Write("Usage notification could not be shown", error); }
        }
    }
}
