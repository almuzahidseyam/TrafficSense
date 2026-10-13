using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace TrafficSense;

internal sealed class PluginBridgeWindow : Window
{
    private readonly UiLanguage _language;
    private readonly Func<double[]> _metrics;
    private readonly Action<PluginSample?> _publish;
    private readonly Func<bool> _dark;
    private readonly Func<string,PluginClient> _createClient;
    private PluginClient? _client;
    private string? _path;
    private readonly Button _open = new() { Style = null, Margin = new(4) }, _stop = new() { Style = null, Margin = new(4) }, _options = new() { Style = null, Margin = new(4) };
    private readonly ComboBox _commands = new() { MinWidth = 100, Margin = new(4) };
    private readonly Button _run = new() { Style = null, Margin = new(4) };
    private readonly Button _reload = new() { Style = null, Margin = new(4) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(8) };
    private readonly StackPanel _items = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _busy;
    internal PluginBridgeWindow(UiLanguage language, Func<double[]> metrics, Action<PluginSample?> publish, Func<bool>? dark = null, Func<string,PluginClient>? createClient = null)
    {
        _language = language; _metrics = metrics; _publish = publish; _dark = dark ?? (()=>true);
        _createClient = createClient ?? (path=>new PluginClient(Environment.ProcessPath!,path));
        Width = 620; Height = 400; MinWidth = 360; MinHeight = 250; FontSize = 14; FontFamily = new FontFamily("Segoe UI, Nirmala UI");
        Title = language[TextKey.PluginBridge]; Background = SystemColors.WindowBrush; Foreground = SystemColors.WindowTextBrush;
        var root = new DockPanel { Margin = new(12) }; var top = new StackPanel();
        top.Children.Add(new TextBlock { Text = language[TextKey.PluginHint], TextWrapping = TextWrapping.Wrap, Margin = new(4) });
        var buttons = new WrapPanel(); foreach (var control in new Control[] { _open, _stop, _reload, _options, _commands, _run }) buttons.Children.Add(control);
        top.Children.Add(buttons); top.Children.Add(_status); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        root.Children.Add(new ScrollViewer { Content = _items, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
        _open.Content = language[TextKey.PluginChoose]; _stop.Content = language[TextKey.BridgeStop]; _options.Content = language[TextKey.PluginOptions]; _run.Content = language[TextKey.PluginRun];
        _reload.Content = language[TextKey.PluginReload];
        _open.Click += async (_, _) => { var dialog = new OpenFileDialog { Filter = "Trusted x64 TrafficMonitor plugin (*.dll)|*.dll" }; if (dialog.ShowDialog(this) == true) await Open(dialog.FileName); };
        _stop.Click += (_, _) => Stop(); _options.Click += async (_, _) => await Poll("options"); _run.Click += async (_, _) => await Poll("command");
        _reload.Click += async (_, _) => { if(_path is {} path) await Open(path); };
        _timer.Tick += async (_, _) => await Poll("sample");
        Closed += (_, _) => { _timer.Stop(); Stop(); }; Controls();
    }
    internal async Task<bool> Open(string path)
    {
        Stop(); _path = path;
        try { _client = _createClient(path); await Poll("sample"); if (_client != null) _timer.Start(); return _client != null; }
        catch (Exception e) when (e is InvalidDataException or IOException or System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        { _status.Text = _language[TextKey.PluginFailed] + " " + e.Message; Stop(clearStatus: false); return false; }
    }
    private void Controls() { _stop.IsEnabled = _client != null; _open.IsEnabled = !_busy; _reload.IsEnabled = _path != null && !_busy; _options.IsEnabled = _client != null && !_busy; _run.IsEnabled = _client != null && !_busy && _commands.SelectedIndex >= 0; }
    private void Stop(bool clearStatus = true)
    { _timer.Stop(); _client?.Dispose(); _client = null; _publish(null); _items.Children.Clear(); _commands.Items.Clear(); if (clearStatus) _status.Text = _language[TextKey.BridgeStopped]; Controls(); }
    private async Task Poll(string operation)
    {
        if (_client == null || _busy) return;
        var client = _client; _busy = true; Controls();
        try
        {
            var sample = await client.Request(new(operation, _metrics(), _commands.SelectedIndex, _dark()), operation == "sample" ? null : TimeSpan.FromMinutes(5));
            if (!ReferenceEquals(client, _client)) return;
            _publish(sample); _status.Text = sample.Name + " · API " + sample.Api + "\n" + sample.Tooltip;
            _items.Children.Clear();
            foreach (var item in sample.Items)
            {
                _items.Children.Add(new TextBlock { Text = item.Name + " · " + item.Label + item.Value, Margin = new(4), TextWrapping = TextWrapping.Wrap });
                if (item.Png != null)
                {
                    var bitmap = PluginImage.Decode(item.Png);
                    _items.Children.Add(new Image { Source = bitmap, Height = 40, HorizontalAlignment = HorizontalAlignment.Left });
                }
            }
            int selected = _commands.SelectedIndex; _commands.Items.Clear(); foreach (string command in sample.Commands) _commands.Items.Add(command);
            _commands.SelectedIndex = sample.Commands.Length == 0 ? -1 : Math.Clamp(selected, 0, sample.Commands.Length - 1);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or System.ComponentModel.Win32Exception)
        {
            if (ReferenceEquals(client, _client)) { _status.Text = e is PluginResourceLimitException ? _language[TextKey.PluginResourceLimit] : _language[TextKey.PluginFailed] + " " + e.Message; Stop(clearStatus: false); }
        }
        finally { _busy = false; Controls(); }
    }
}

internal static class PluginImage
{
    internal static BitmapSource Decode(string encoded)
    {
        if (encoded.Length > 192*1024) throw new InvalidDataException("Plugin image too large.");
        byte[] data = Convert.FromBase64String(encoded);
        if (data.Length < 24 || !data.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) throw new InvalidDataException("Invalid plugin PNG.");
        int width=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16,4)), height=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20,4));
        if(width is <1 or >512 || height is <1 or >64) throw new InvalidDataException("Plugin image dimensions exceed limits.");
        using var bytes=new MemoryStream(data); var bitmap=BitmapDecoder.Create(bytes,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];
        if(bitmap.PixelWidth!=width || bitmap.PixelHeight!=height)throw new InvalidDataException("Inconsistent PNG dimensions.");
        bitmap.Freeze(); return bitmap;
    }
}
