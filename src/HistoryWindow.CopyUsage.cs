using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TrafficSense;

internal sealed partial class HistoryWindow
{
    private readonly TextBlock _copyDayLabel = new() { Style = null, TextWrapping = TextWrapping.Wrap };
    private readonly Button _copyDay = new()
    {
        Style = null, TabIndex = 5, HorizontalAlignment = HorizontalAlignment.Left,
        Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 6),
        Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush, BorderBrush = SystemColors.ActiveBorderBrush
    };
    private readonly TextBlock _copyDayStatus = new() { Style = null, Foreground = SystemColors.WindowTextBrush,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
    private UsageDay? _lastCopyDay;
    private string? _lastCopyLanguage;
    private bool _copySucceeded;

    private void InitializeCopyUsage(Panel panel)
    {
        _copyDay.Content = _copyDayLabel;
        ToolTipService.SetShowOnDisabled(_copyDay, true);
        AutomationProperties.SetLiveSetting(_copyDayStatus, AutomationLiveSetting.Polite);
        _copyDay.Click += (_, _) => CopySelectedDay();
        panel.Children.Add(_copyDay); panel.Children.Add(_copyDayStatus);
    }

    private void RefreshCopyUsage()
    {
        var row = _dailyUsage.SelectedItem as UsageDayRow;
        _copyDayLabel.Text = _language[TextKey.CopyDay];
        _copyDay.IsEnabled = row != null;
        string help = _language[row == null ? TextKey.CopyDaySelect : TextKey.CopyDayHint];
        _copyDay.ToolTip = help;
        AutomationProperties.SetName(_copyDay, _copyDayLabel.Text);
        AutomationProperties.SetHelpText(_copyDay, help);
        if (row == null || row.Day != _lastCopyDay || _language.Code != _lastCopyLanguage)
        { _lastCopyDay = null; _lastCopyLanguage = null; }
        string status = _lastCopyDay == null ? "" : _copySucceeded
            ? string.Format(CultureInfo.InvariantCulture, _language[TextKey.CopyDayDone], row!.Date)
            : _language[TextKey.CopyDayFailed];
        bool changed = _copyDayStatus.Text != status;
        _copyDayStatus.Text = status;
        _copyDayStatus.Visibility = status.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(_copyDayStatus, status);
        if (changed && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            UIElementAutomationPeer.CreatePeerForElement(_copyDayStatus)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    internal bool CopySelectedDay()
    {
        // Include pending traffic and revalidate filtering before taking the
        // snapshot. Merely selecting/copying never marks usage saved.
        Refresh();
        if (_dailyUsage.SelectedItem is not UsageDayRow row) return false;
        _lastCopyDay = row.Day; _lastCopyLanguage = _language.Code;
        try
        {
            _copyText(row.Details.Replace("\n", "\r\n"));
            _copySucceeded = true;
        }
        catch (Exception error) when (error is ExternalException or InvalidOperationException or System.Threading.ThreadStateException)
        {
            _diagnostics?.Write("UsageCopy", error);
            _copySucceeded = false;
        }
        RefreshCopyUsage();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_copyDayStatus.IsVisible && _lastCopyDay != null) _copyDayStatus.BringIntoView();
        }), DispatcherPriority.Loaded);
        return _copySucceeded;
    }
}
