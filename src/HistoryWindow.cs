using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TrafficSense;

internal sealed class HistoryWindow : Window
{
    private readonly UiLanguage _language;
    private readonly UsageLedger _usage;
    private readonly Func<DateTimeOffset> _localTime;
    private readonly SpeedHistory _history;
    private readonly Func<bool> _bits;
    private readonly SpeedGraph _graph = new() { Height = 170, Margin = new(0, 0, 0, 12) };
    private readonly TextBlock _legend = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 10) };
    private readonly TextBlock _today = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 12) };
    private readonly TextBlock _month = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 12) };
    private readonly TextBlock _hint = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 16) };
    private readonly Button _close = new() { MinWidth = 90, HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true,
        Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush, BorderBrush = SystemColors.ActiveBorderBrush };

    internal HistoryWindow(UiLanguage language, UsageLedger usage, Func<DateTimeOffset> localTime, SpeedHistory history, Func<bool> bits)
    {
        _language = language; _usage = usage; _localTime = localTime;
        _history = history; _bits = bits;
        Width = 480; MinWidth = 360; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.CanMinimize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI, Nirmala UI, Kalpurush"); FontSize = 14;
        Background = SystemColors.WindowBrush; Foreground = SystemColors.WindowTextBrush;
        var panel = new StackPanel { Margin = new Thickness(20), Background = SystemColors.WindowBrush };
        panel.Children.Add(_today); panel.Children.Add(_month); panel.Children.Add(_legend); panel.Children.Add(_graph);
        panel.Children.Add(_hint); panel.Children.Add(_close);
        Content = panel;
        _close.Click += (_, _) => Close();
        _language.Changed += LanguageChanged;
        Closed += (_, _) => _language.Changed -= LanguageChanged;
        Refresh();
    }

    private void LanguageChanged(object? sender, EventArgs e) => Refresh();
    internal void Refresh()
    {
        Title = _language[TextKey.History];
        var date = DateOnly.FromDateTime(_localTime().Date);
        _today.Text = Summary(TextKey.Today, _usage.Today(date));
        _month.Text = Summary(TextKey.ThisMonth, _usage.Month(date));
        _hint.Text = _language[TextKey.UsageHint];
        var latest = _history.Samples.LastOrDefault().Speed;
        _legend.Text = _language[TextKey.GraphLegend] + "\n" + _language[TextKey.Download] + ": " +
            SpeedFormatter.Format(latest.DownloadBytesPerSecond, _bits()) + "    " + _language[TextKey.Upload] + ": " +
            SpeedFormatter.Format(latest.UploadBytesPerSecond, _bits());
        System.Windows.Automation.AutomationProperties.SetName(_graph, _legend.Text);
        _graph.Update(_history.Samples, _bits());
        _close.Content = _language[TextKey.Close];
    }

    private string Summary(TextKey key, UsageDay day) => _language[key] + "\n" +
        _language[TextKey.Download] + ": " + FormatBytes(day.Received) + "    " +
        _language[TextKey.Upload] + ": " + FormatBytes(day.Sent);
    internal static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / (1024d * 1024 * 1024):F2} GB";
        if (bytes >= 1024 * 1024) return $"{bytes / (1024d * 1024):F2} MB";
        if (bytes >= 1024) return $"{bytes / 1024d:F2} KB";
        return $"{bytes} B";
    }
}
