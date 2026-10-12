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
    private readonly TextBlock _copyFormatLabel = new() { Style = null, TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox _copyFormat = new() { Style = null, TabIndex = 5, MinWidth = 160,
        HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 6) };
    private readonly Button _copyDay = new()
    {
        Style = null, TabIndex = 6, HorizontalAlignment = HorizontalAlignment.Left,
        Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 6),
        Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush, BorderBrush = SystemColors.ActiveBorderBrush
    };
    private readonly TextBlock _copyDayStatus = new() { Style = null, Foreground = SystemColors.WindowTextBrush,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
    private UsageDay? _lastCopyDay;
    private string? _lastCopyLanguage;
    private bool _lastCopyCsv;
    private bool _copySucceeded;

    private void InitializeCopyUsage(Panel panel)
    {
        _copyFormat.Items.Add(new ComboBoxItem { Tag = false });
        _copyFormat.Items.Add(new ComboBoxItem { Tag = true });
        _copyFormat.SelectedIndex = 0;
        _copyFormat.SelectionChanged += (_, _) => RefreshCopyUsage();
        _copyDay.Content = _copyDayLabel;
        ToolTipService.SetShowOnDisabled(_copyDay, true);
        AutomationProperties.SetLiveSetting(_copyDayStatus, AutomationLiveSetting.Polite);
        _copyDay.Click += (_, _) => CopySelectedDay();
        panel.Children.Add(_copyFormatLabel); panel.Children.Add(_copyFormat);
        panel.Children.Add(_copyDay); panel.Children.Add(_copyDayStatus);
    }

    private void RefreshCopyUsage()
    {
        var row = _dailyUsage.SelectedItem as UsageDayRow;
        bool csv = CopyAsCsv;
        _copyFormatLabel.Text = _language[TextKey.CopyFormat];
        foreach (ComboBoxItem item in _copyFormat.Items)
            item.Content = _language[(bool)item.Tag ? TextKey.CopyCsv : TextKey.CopyPlainText];
        AutomationProperties.SetName(_copyFormat, _copyFormatLabel.Text);
        _copyDayLabel.Text = _language[TextKey.CopyDay];
        _copyDay.IsEnabled = row != null;
        string help = _language[row == null ? TextKey.CopyDaySelect : csv ? TextKey.CopyCsvHint : TextKey.CopyDayHint];
        _copyDay.ToolTip = help;
        AutomationProperties.SetName(_copyDay, _copyDayLabel.Text);
        AutomationProperties.SetHelpText(_copyDay, help);
        if (row == null || row.Day != _lastCopyDay || _language.Code != _lastCopyLanguage || csv != _lastCopyCsv)
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

    private bool CopyAsCsv => _copyFormat.SelectedItem is ComboBoxItem { Tag: true };

    internal bool CopySelectedDay()
    {
        // Include pending traffic and revalidate filtering before taking the
        // snapshot. Merely selecting/copying never marks usage saved.
        Refresh();
        if (_dailyUsage.SelectedItem is not UsageDayRow row) return false;
        _lastCopyDay = row.Day; _lastCopyLanguage = _language.Code; _lastCopyCsv = CopyAsCsv;
        try
        {
            _copyText(_lastCopyCsv ? UsageCsv.CopyDay(row.Day) : row.Details.Replace("\n", "\r\n"));
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
