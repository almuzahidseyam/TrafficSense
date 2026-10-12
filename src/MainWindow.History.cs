using System.Windows;

namespace TrafficSense;

public partial class MainWindow
{
    private readonly UsageStore? _usageStore;
    private readonly UsageLedger _usage;
    private readonly Func<DateTimeOffset> _localTime;
    private readonly System.Diagnostics.Stopwatch _usageSaveClock = System.Diagnostics.Stopwatch.StartNew();
    private HistoryWindow? _historyWindow;
    private readonly SpeedHistory _speedHistory = new();

    private void MergeSampledUsage(SamplingDelivery delivery)
    {
        foreach (var day in delivery.Usage)
            _usage.Record(new(day.Received, day.Sent), new DateTimeOffset(day.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
    }

    private void RecordTraffic(TimeSpan timestamp)
    {
        _usage.Record(_speedCalculator.LastTraffic, _localTime());
        RecordHistory(timestamp);
    }

    private void RecordHistory(TimeSpan timestamp)
    {
        CheckUsageLimits();
        _speedHistory.Add(timestamp, _latestSpeed);
        _historyWindow?.Refresh();
        if (_usageSaveClock.Elapsed >= TimeSpan.FromSeconds(60)) FlushUsage();
    }

    private void FlushUsage()
    {
        if (_usage.Dirty) _usageStore?.Save(_usage);
        _usageSaveClock.Restart();
    }

    private void MenuHistory_Click(object sender, RoutedEventArgs e) => ShowHistory();
    internal void ShowHistory()
    {
        if (_fatalShutdown || Volatile.Read(ref _monitoringClosed) != 0) return;
        if (_historyWindow == null)
        {
            _historyWindow = new HistoryWindow(Localization, _usage, _localTime, _speedHistory, () => _settings.SpeedInBits,
                diagnostics: _diagnostics, reportError: _reportError);
            _historyWindow.Closed += (_, _) => _historyWindow = null;
        }
        if (_historyWindow.WindowState == WindowState.Minimized) _historyWindow.WindowState = WindowState.Normal;
        _historyWindow.Show();
        if (_historyWindow.ShowActivated) _historyWindow.Activate();
    }
    private void CloseHistory() => _historyWindow?.Close();
}
