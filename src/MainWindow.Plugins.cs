using System.Windows;

namespace TrafficSense;

public partial class MainWindow
{
    private PluginBridgeWindow? _pluginBridge;
    private PluginSample? _pluginSample;
    private long _pluginUpdated;
    private readonly Dictionary<string,(string Encoded,System.Windows.Media.Imaging.BitmapSource Image)> _pluginImages = [];
    private void MenuPluginBridge_Click(object sender, RoutedEventArgs e)
    {
        if (_pluginBridge == null)
        {
            _pluginBridge = new(Localization, PluginMetrics, sample => { _pluginSample = sample; _pluginUpdated = System.Diagnostics.Stopwatch.GetTimestamp(); if (sample == null) _pluginImages.Clear(); if (IsVisible) RenderLayoutSkin(); },()=>_settings.Theme!="Light");
            _pluginBridge.Closed += (_, _) => { _pluginBridge = null; _pluginSample = null; ConfigureSystemMetrics(); RenderLayoutSkin(); };
            ConfigureSystemMetrics();
        }
        _pluginBridge.Show(); _pluginBridge.Activate();
    }
    private double[] PluginMetrics()
    {
        var metrics = _systemService?.Latest ?? new(); bool fresh = _sampleQuality == SampleQuality.Fresh;
        var today = _usage.Today(DateOnly.FromDateTime(_localTime().Date));
        return [fresh ? _latestSpeed.UploadBytesPerSecond : -1, fresh ? _latestSpeed.DownloadBytesPerSecond : -1,
            metrics.CpuPercent ?? -1, metrics.MemoryPercent ?? -1, metrics.GpuPercent ?? -1, -1, -1, -1, -1, metrics.DiskPercent ?? -1, metrics.CpuMhz ?? -1, today.Sent, today.Received];
    }
    private string PluginValue(string id)
    {
        if (_pluginSample == null || System.Diagnostics.Stopwatch.GetElapsedTime(_pluginUpdated) > TimeSpan.FromSeconds(5)) return Localization[TextKey.NotAvailable];
        return _pluginSample.Items.FirstOrDefault(item => item.Id == id)?.Value ?? Localization[TextKey.NotAvailable];
    }
    private System.Windows.Media.Imaging.BitmapSource? PluginImageFor(string id)
    {
        if (_pluginSample == null || System.Diagnostics.Stopwatch.GetElapsedTime(_pluginUpdated) > TimeSpan.FromSeconds(5)) return null;
        string? png = _pluginSample.Items.FirstOrDefault(item => item.Id == id)?.Png; if (png == null) return null;
        if (_pluginImages.TryGetValue(id,out var cached) && cached.Encoded == png) return cached.Image;
        var bitmap = PluginImage.Decode(png); if (_pluginImages.Count >= 32) _pluginImages.Clear(); _pluginImages[id] = (png,bitmap); return bitmap;
    }
}
