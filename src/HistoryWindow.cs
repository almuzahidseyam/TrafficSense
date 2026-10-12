using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace TrafficSense;

internal sealed partial class HistoryWindow : Window
{
    private readonly UiLanguage _language;
    private readonly UsageLedger _usage;
    private readonly Func<DateTimeOffset> _localTime;
    private readonly SpeedHistory _history;
    private readonly Func<bool> _bits;
    private readonly DiagnosticLog? _diagnostics;
    private readonly Action<string>? _reportError;
    private readonly Func<string?> _chooseExportPath;
    private readonly SpeedGraph _graph = new() { Height = 170, Margin = new(0, 0, 0, 12) };
    private readonly TextBlock _legend = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 10) };
    private readonly TextBlock _today = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 12) };
    private readonly TextBlock _month = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 12) };
    private readonly TextBlock _hint = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 16) };
    private readonly Button _close = new() { MinWidth = 90, HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true, TabIndex = 5, Style = null,
        Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush, BorderBrush = SystemColors.ActiveBorderBrush };
    private readonly Button _export = new() { MinWidth = 120, Margin = new(0, 0, 12, 0), TabIndex = 4, Style = null,
        Background = SystemColors.ControlBrush, Foreground = SystemColors.ControlTextBrush, BorderBrush = SystemColors.ActiveBorderBrush };

    internal HistoryWindow(UiLanguage language, UsageLedger usage, Func<DateTimeOffset> localTime, SpeedHistory history, Func<bool> bits,
        DiagnosticLog? diagnostics = null, Action<string>? reportError = null, Func<string?>? chooseExportPath = null)
    {
        _language = language; _usage = usage; _localTime = localTime;
        _history = history; _bits = bits;
        _diagnostics = diagnostics; _reportError = reportError; _chooseExportPath = chooseExportPath ?? ChooseExportPath;
        Width = 480; MinWidth = 360; SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height;
        ResizeMode = ResizeMode.CanMinimize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI, Nirmala UI, Kalpurush"); FontSize = 14;
        Background = SystemColors.WindowBrush; Foreground = SystemColors.WindowTextBrush;
        var panel = new StackPanel { Margin = new Thickness(20), Background = SystemColors.WindowBrush };
        panel.Children.Add(_today); panel.Children.Add(_month); panel.Children.Add(_legend); panel.Children.Add(_graph);
        panel.Children.Add(_hint);
        InitializeExportFilter(panel);
        var buttons = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(_close, Dock.Right); DockPanel.SetDock(_export, Dock.Left);
        buttons.Children.Add(_close); buttons.Children.Add(_export); panel.Children.Add(buttons);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _close.Click += (_, _) => Close();
        ToolTipService.SetShowOnDisabled(_export, true);
        _export.Click += (_, _) =>
        {
            UpdateExportFilter();
            if (!_export.IsEnabled) return;
            string? path = _chooseExportPath();
            if (path != null) ExportUsage(path);
        };
        SizeChanged += (_, _) => QueueHistoryPlacement();
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
        _export.Content = _language[TextKey.CsvExport];
        RefreshExportFilter(date);
    }

    private string? ChooseExportPath()
    {
        var dialog = new SaveFileDialog
        {
            Title = _language[TextKey.CsvExport], Filter = "CSV (*.csv)|*.csv", DefaultExt = ".csv", AddExtension = true,
            FileName = "TrafficSense-usage-" + DateOnly.FromDateTime(_localTime().Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".csv",
            OverwritePrompt = true
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    internal bool ExportUsage(string path)
    {
        UpdateExportFilter();
        if (!_export.IsEnabled || !TryGetExportRange(out var range)) return false;
        var result = UsageCsv.Export(path, _usage, range);
        if (result.Success) return true;
        _diagnostics?.Write("UsageExport", result.Error);
        string message = _language[TextKey.CsvExportFailed]
            + (string.IsNullOrWhiteSpace(result.Error) ? "" : "\n\n" + result.Error);
        if (_reportError != null) _reportError(message);
        else MessageBox.Show(this, message, _language[TextKey.ErrorTitle], MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private string Summary(TextKey key, UsageDay day) => _language[key] + "\n" +
        _language[TextKey.Download] + ": " + FormatBytes(day.Received) + "    " +
        _language[TextKey.Upload] + ": " + FormatBytes(day.Sent);
    internal static string FormatBytes(decimal bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / (1024m * 1024 * 1024):F2} GB";
        if (bytes >= 1024 * 1024) return $"{bytes / (1024m * 1024):F2} MB";
        if (bytes >= 1024) return $"{bytes / 1024m:F2} KB";
        return $"{bytes} B";
    }
}
