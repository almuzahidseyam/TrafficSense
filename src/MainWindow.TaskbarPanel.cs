using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace TrafficSense;

public partial class MainWindow
{
    private readonly IAppBarShell? _appBarShell;
    private TaskbarPanelWindow? _taskbarPanel;
    private EmbeddedTaskbarClient? _embedded;
    private bool _embeddingBusy, _embeddedActive;
    private DateTimeOffset _embeddingNext;
    private string? _embeddingStatus;

    private void ApplyPanelLanguage()
    {
        MenuTaskbarPanel.Header = Localization[TextKey.TaskbarPanel];
        MenuTaskbarPanel.IsChecked = _settings.TaskbarPanel;
        MenuTaskbarPanel.ToolTip = Localization[TextKey.PanelHint];
        MenuEmbedTaskbar.Header = Localization[TextKey.EmbedTaskbar]; MenuEmbedTaskbar.IsChecked = _settings.EmbedTaskbar;
        MenuEmbedTaskbar.ToolTip = Localization[TextKey.EmbedHint] + "\n" + _embeddingStatus;
        MenuPanelTop.Header = Localization[TextKey.PanelTop]; MenuPanelTop.IsChecked = _settings.PanelAtTop;
        MenuPanelMonitor.Header = Localization[TextKey.PanelMonitor];
        RefreshTaskbarPanel();
    }

    internal bool ChangeTaskbarPanel(bool enabled, bool? top = null, string? monitor = null, bool changeMonitor = false)
    {
        var previous = (_settings.TaskbarPanel, _settings.PanelAtTop, _settings.PanelMonitor);
        _settings.TaskbarPanel = enabled;
        if (top.HasValue) _settings.PanelAtTop = top.Value;
        if (changeMonitor) _settings.PanelMonitor = monitor;
        var result = _saveSettings(_settings);
        if (!result.Success)
        {
            (_settings.TaskbarPanel, _settings.PanelAtTop, _settings.PanelMonitor) = previous;
            ShowSettingsError(result.Error); ApplyPanelLanguage(); return false;
        }
        ConfigureTaskbarPanel(); ApplyPanelLanguage(); return _settings.TaskbarPanel == enabled;
    }

    private DockBounds PanelMonitorBounds()
    {
        var screen = _settings.PanelMonitor == null ? Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle) :
            Forms.Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName == _settings.PanelMonitor) ?? Forms.Screen.PrimaryScreen!;
        return new(screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Right, screen.Bounds.Bottom);
    }

    private void ConfigureTaskbarPanel()
    {
        if (!_settings.TaskbarPanel || !_settings.EmbedTaskbar) { _embedded?.Dispose(); _embedded = null; _embeddedActive = false; }
        if (!_settings.TaskbarPanel || _fatalShutdown || Volatile.Read(ref _monitoringClosed) != 0)
        { _taskbarPanel?.ReleaseAndClose(); _taskbarPanel = null; return; }
        if (_settings.EmbedTaskbar && _embeddedActive) { _taskbarPanel?.ReleaseAndClose(); _taskbarPanel = null; return; }
        if (_taskbarPanel != null) { _taskbarPanel.Reposition(); return; }
        _taskbarPanel = new(_appBarShell ?? new AppBarShell(), PanelMonitorBounds, ShowHistory, () =>
        {
            var menu = ((Border)Content).ContextMenu; menu.PlacementTarget = _taskbarPanel; menu.IsOpen = true;
        }, () => ChangeTaskbarPanel(false));
        RefreshTaskbarPanel();
        if (!_taskbarPanel.TryShow())
        {
            _taskbarPanel.ReleaseAndClose(); _taskbarPanel = null; _settings.TaskbarPanel = false;
            var rollback = _saveSettings(_settings); _diagnostics?.Write("TaskbarPanel", "Appbar registration failed. " + rollback.Error);
            ShowSettingsError(null, TextKey.PanelUnavailable);
        }
    }

    private void RefreshTaskbarPanel()
    {
        RefreshEmbedded();
        if (_taskbarPanel == null) return;
        var metrics = _systemService?.Latest ?? new();
        string lines = string.Join("\n", MetricLabels.Where(label => (((SystemMetric)_settings.DisplayMetrics) & label.Metric) != 0)
            .Select(label => Localization[label.Label] + ": " + MetricText(label.Metric, metrics)));
        _taskbarPanel.Update(SpeedFormatter.Format(_latestSpeed.DownloadBytesPerSecond, _settings.SpeedInBits),
            SpeedFormatter.Format(_latestSpeed.UploadBytesPerSecond, _settings.SpeedInBits), lines, Localization,
            _settings.PanelAtTop, WidgetSkin.From(_settings), _settings.Theme == "Light");
    }

    private async void RefreshEmbedded()
    {
        if (!_settings.TaskbarPanel || !_settings.EmbedTaskbar || _embeddingBusy || DateTimeOffset.UtcNow < _embeddingNext || _appBarShell != null || _fatalShutdown || Volatile.Read(ref _monitoringClosed) != 0) return;
        _embeddingBusy = true; _embeddingNext = DateTimeOffset.UtcNow.AddSeconds(1);
        EmbeddedTaskbarClient? client = null;
        try
        {
            client = _embedded ??= new(); var bounds = PanelMonitorBounds();
            string text = "↓ " + SpeedFormatter.Format(_latestSpeed.DownloadBytesPerSecond, _settings.SpeedInBits) + "   ↑ " + SpeedFormatter.Format(_latestSpeed.UploadBytesPerSecond, _settings.SpeedInBits);
            if (_sampleQuality != SampleQuality.Fresh) text += " · " + Localization[_sampleQuality == SampleQuality.Unavailable ? TextKey.SampleUnavailableShort : _sampleQuality == SampleQuality.Delayed ? TextKey.SampleDelayedShort : TextKey.SamplingShort];
            var reply = await client.Update(new(new(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom), text, _settings.Theme == "Light"));
            if (!ReferenceEquals(client, _embedded)) return;
            bool changed = _embeddedActive != reply.Embedded; _embeddedActive = reply.Embedded; _embeddingStatus = reply.Reason;
            MenuEmbedTaskbar.ToolTip = Localization[TextKey.EmbedHint] + "\n" + reply.Reason;
            if (changed) ConfigureTaskbarPanel();
            if (reply.Action == "history") ShowHistory();
            if (reply.Action == "settings") { var menu = ((Border)Content).ContextMenu; menu.PlacementTarget = this; menu.IsOpen = true; }
            if (!reply.Embedded) { _embedded.Dispose(); _embedded = null; _embeddingNext = DateTimeOffset.UtcNow.AddSeconds(30); }
        }
        catch (Exception e) when (e is System.IO.InvalidDataException or System.IO.IOException or System.ComponentModel.Win32Exception or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException or ArgumentException)
        {
            if (client == null || ReferenceEquals(client, _embedded))
            { _embedded?.Dispose(); _embedded = null; _embeddedActive = false; _embeddingNext = DateTimeOffset.UtcNow.AddSeconds(30); _diagnostics?.Write("TaskbarEmbedding", e); ConfigureTaskbarPanel(); }
        }
        finally { _embeddingBusy = false; }
    }
    private void MenuEmbedTaskbar_Click(object sender, RoutedEventArgs e)
    {
        bool previous = _settings.EmbedTaskbar; _settings.EmbedTaskbar = MenuEmbedTaskbar.IsChecked;
        var result = _saveSettings(_settings); if (!result.Success) { _settings.EmbedTaskbar = previous; ShowSettingsError(result.Error); }
        _embeddingNext = default; ConfigureTaskbarPanel(); ApplyPanelLanguage();
    }

    private void MenuTaskbarPanel_Click(object sender, RoutedEventArgs e) { ChangeTaskbarPanel(MenuTaskbarPanel.IsChecked); e.Handled = true; }
    private void MenuPanelTop_Click(object sender, RoutedEventArgs e) { ChangeTaskbarPanel(_settings.TaskbarPanel, MenuPanelTop.IsChecked); e.Handled = true; }
    private void MenuPanelMonitor_Opened(object sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, MenuPanelMonitor)) return;
        MenuPanelMonitor.Items.Clear();
        void Add(string label, string? id)
        {
            var item = new MenuItem { Header = label.Replace("_", "__"), IsCheckable = true, IsChecked = _settings.PanelMonitor == id };
            item.Click += (_, args) => { ChangeTaskbarPanel(_settings.TaskbarPanel, monitor: id, changeMonitor: true); args.Handled = true; };
            MenuPanelMonitor.Items.Add(item);
        }
        Add(Localization[TextKey.PanelFollowWidget], null);
        foreach (var screen in Forms.Screen.AllScreens) Add(screen.DeviceName + $" ({screen.Bounds.Width} × {screen.Bounds.Height})", screen.DeviceName);
    }
}
