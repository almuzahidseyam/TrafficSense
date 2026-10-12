using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace TrafficSense;

public partial class MainWindow
{
    private readonly Func<SystemMetric, SystemMetrics>? _readSystemMetrics;
    private SystemMetricsService? _systemService;
    private SystemMetric _systemMask;
    private Window? _systemWindow;
    private TextBox? _systemDetails;
    private static readonly (SystemMetric Metric, TextKey Label)[] MetricLabels =
    [ (SystemMetric.Cpu, TextKey.Cpu), (SystemMetric.Memory, TextKey.Memory), (SystemMetric.Frequency, TextKey.CpuFrequency),
      (SystemMetric.Gpu, TextKey.Gpu), (SystemMetric.Disk, TextKey.Disk) ];

    private void InitializeSystemMetrics()
    {
        _settings.DisplayMetrics &= (int)SystemMetric.All;
        foreach (var (metric, _) in MetricLabels)
        {
            var item = new MenuItem { Tag = metric, IsCheckable = true };
            item.Click += (_, e) => { ChangeDisplayedMetrics((SystemMetric)_settings.DisplayMetrics ^ metric); e.Handled = true; };
            MenuMetrics.Items.Add(item);
        }
        ConfigureSystemMetrics();
    }

    internal bool ChangeDisplayedMetrics(SystemMetric metrics)
    {
        int previous = _settings.DisplayMetrics;
        _settings.DisplayMetrics = (int)(metrics & SystemMetric.All);
        var result = _saveSettings(_settings);
        if (!result.Success) { _settings.DisplayMetrics = previous; ShowSettingsError(result.Error); }
        ConfigureSystemMetrics(); ApplySystemLanguage();
        return result.Success;
    }

    private void ConfigureSystemMetrics()
    {
        if (Volatile.Read(ref _monitoringClosed) != 0) return;
        var mask = (SystemMetric)_settings.DisplayMetrics | (_systemWindow != null ? SystemMetric.All : SystemMetric.None);
        if (mask == _systemMask) return;
        _systemService?.Dispose(); _systemService = null;
        _systemMask = mask;
        if (mask != SystemMetric.None) _systemService = new(mask, _diagnostics, _readSystemMetrics);
    }

    private void ApplySystemLanguage()
    {
        MenuMetrics.Header = Localization[TextKey.DisplayMetrics];
        MenuMetrics.ToolTip = Localization[TextKey.MetricsHint];
        MenuSystemDetails.Header = Localization[TextKey.SystemDetails];
        foreach (MenuItem item in MenuMetrics.Items)
        {
            var metric = (SystemMetric)item.Tag;
            item.Header = Localization[MetricLabels.Single(label => label.Metric == metric).Label];
            item.IsChecked = ((SystemMetric)_settings.DisplayMetrics).HasFlag(metric);
        }
        RefreshSystemMetrics();
    }

    private string MetricText(SystemMetric metric, SystemMetrics metrics)
    {
        string Number(double? value, string suffix) => MetricMath.Valid(value) is { } number
            ? number.ToString("F1", CultureInfo.CurrentCulture) + suffix : Localization[TextKey.NotAvailable];
        return metric switch
        {
            SystemMetric.Cpu => Number(metrics.CpuPercent, "%"),
            SystemMetric.Memory => Number(metrics.MemoryPercent, "%"),
            SystemMetric.Frequency => Number(metrics.CpuMhz / 1000, " GHz"),
            SystemMetric.Gpu => Number(metrics.GpuPercent, "%"),
            SystemMetric.Disk => Number(metrics.DiskPercent, "%") + "  R " +
                (metrics.DiskReadBytes.HasValue ? SpeedFormatter.Format(metrics.DiskReadBytes.Value) : Localization[TextKey.NotAvailable]) + "  W " +
                (metrics.DiskWriteBytes.HasValue ? SpeedFormatter.Format(metrics.DiskWriteBytes.Value) : Localization[TextKey.NotAvailable]),
            _ => ""
        };
    }

    private void RefreshSystemMetrics()
    {
        var metrics = _systemService?.Latest ?? new();
        if (IsVisible)
        {
            var lines = MetricLabels.Where(label => (((SystemMetric)_settings.DisplayMetrics) & label.Metric) != 0)
                .Select(label => Localization[label.Label] + ": " + MetricText(label.Metric, metrics));
            TxtMetrics.Text = string.Join("\n", lines);
            TxtMetrics.Visibility = TxtMetrics.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            TxtMetrics.ToolTip = Localization[TextKey.MetricsHint];
            AutomationProperties.SetName(TxtMetrics, TxtMetrics.Text);
        }
        if (_systemWindow == null || _systemDetails == null) return;
        _systemWindow.Title = Localization[TextKey.SystemDetails];
        string memory = metrics.TotalMemory.HasValue && metrics.AvailableMemory.HasValue
            ? HistoryWindow.FormatBytes((decimal)metrics.TotalMemory.Value - metrics.AvailableMemory.Value) + " / " + HistoryWindow.FormatBytes(metrics.TotalMemory.Value)
            : Localization[TextKey.NotAvailable];
        string connection = _monitoredAdapter == null ? Localization[TextKey.NoConnectedAdapter] :
            _monitoredAdapter.Name + "\n" + _monitoredAdapter.Description + "\n" + _monitoredAdapter.Addresses +
            "\n" + Localization[TextKey.LinkSpeed] + ": " + SpeedFormatter.Format(_monitoredAdapter.LinkBitsPerSecond / 8d, true) +
            "\n" + Localization[TextKey.InterfaceCounters] + ": " + _monitoredAdapter.Snapshot?.BytesReceived.ToString(CultureInfo.InvariantCulture) +
            " / " + _monitoredAdapter.Snapshot?.BytesSent.ToString(CultureInfo.InvariantCulture);
        string text = string.Join("\n", MetricLabels.Select(label => Localization[label.Label] + ": " + MetricText(label.Metric, metrics))) +
            "\n" + Localization[TextKey.Memory] + ": " + memory + "\n\n" + connection + "\n\n" +
            Localization[TextKey.MetricsHint] + "\n\n" + Localization[TextKey.MeasurementHint];
        if (_systemDetails.Text != text) _systemDetails.Text = text;
        AutomationProperties.SetName(_systemDetails, Localization[TextKey.SystemDetails]);
    }

    private void MenuSystemDetails_Click(object sender, RoutedEventArgs e) => ShowSystemDetails();

    internal void ShowSystemDetails()
    {
        if (_fatalShutdown || Volatile.Read(ref _monitoringClosed) != 0) return;
        if (_systemWindow == null)
        {
            _systemDetails = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(16), Style = null };
            _systemWindow = new SystemDetailsWindow { Width = 520, Height = Math.Min(640, SystemParameters.WorkArea.Height), MinWidth = 320,
                Content = _systemDetails, FontSize = 14, FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Nirmala UI"),
                WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = SystemColors.WindowBrush, Foreground = SystemColors.WindowTextBrush };
            _systemWindow.Closed += (_, _) => { _systemWindow = null; _systemDetails = null; ConfigureSystemMetrics(); };
            ConfigureSystemMetrics();
        }
        RefreshSystemMetrics();
        if (_systemWindow.WindowState == WindowState.Minimized) _systemWindow.WindowState = WindowState.Normal;
        _systemWindow.Show(); if (_systemWindow.ShowActivated) _systemWindow.Activate();
    }
}

internal sealed class SystemDetailsWindow : Window { }
