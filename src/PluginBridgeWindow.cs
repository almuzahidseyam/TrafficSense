using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Globalization;
using System.Windows.Automation;
using System.Diagnostics;

namespace TrafficSense;

internal sealed class PluginBridgeWindow : Window
{
    private readonly UiLanguage _language;
    private readonly Func<double[]> _metrics;
    private readonly Action<PluginSample?> _publish;
    private readonly Func<bool> _dark;
    private readonly Func<string,PluginClient> _createClient;
    private readonly Func<PluginClient,PluginWorkerHealth> _readHealth;
    private PluginClient? _client;
    private string? _path;
    private readonly Button _open = new() { Style = null, Margin = new(4) }, _stop = new() { Style = null, Margin = new(4) }, _options = new() { Style = null, Margin = new(4) };
    private readonly ComboBox _commands = new() { MinWidth = 100, Margin = new(4) };
    private readonly Button _run = new() { Style = null, Margin = new(4) };
    private readonly Button _reload = new() { Style = null, Margin = new(4) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(8) };
    private readonly TextBlock _hint = new() { TextWrapping=TextWrapping.Wrap,Margin=new(4) };
    private readonly TextBlock _healthState = new() { TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold };
    private readonly TextBlock _healthUsage = new() { TextWrapping=TextWrapping.Wrap,Margin=new(0,4,0,0) };
    private readonly TextBlock _healthAge = new() { TextWrapping=TextWrapping.Wrap,Margin=new(0,4,0,0) };
    private readonly PluginHealthHistory _healthHistory=new();
    private readonly PluginHealthGraph _healthGraph=new(){Height=180,MinWidth=180,Margin=new(0,4,0,0)};
    private readonly TextBlock _historyTitle=new(){TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,0)};
    private long _historyStart;
    private readonly StackPanel _items = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _healthTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private PluginWorkerHealth? _latestHealth;
    private TextKey _terminalHealth=TextKey.PluginHealthIdle;
    private string? _operation;
    private bool _healthBusy;
    private bool _healthReadFailed;
    private Exception? _failure;
    private bool _busy;
    internal PluginBridgeWindow(UiLanguage language, Func<double[]> metrics, Action<PluginSample?> publish, Func<bool>? dark = null, Func<string,PluginClient>? createClient = null, Func<PluginClient,PluginWorkerHealth>? readHealth = null)
    {
        _language = language; _metrics = metrics; _publish = publish; _dark = dark ?? (()=>true);
        _createClient = createClient ?? (path=>new PluginClient(Environment.ProcessPath!,path));
        _readHealth = readHealth ?? (client=>client.ReadHealth());
        Width = 620; Height = 640; MinWidth = 360; MinHeight = 250; FontSize = 14; FontFamily = new FontFamily("Segoe UI, Nirmala UI");
        Title = language[TextKey.PluginBridge]; Background = SystemColors.WindowBrush; Foreground = SystemColors.WindowTextBrush;
        var top = new StackPanel { Margin=new(12) };
        top.Children.Add(_hint);
        var buttons = new WrapPanel(); foreach (var control in new Control[] { _open, _stop, _reload, _options, _commands, _run }) buttons.Children.Add(control);
        top.Children.Add(buttons);
        var health = new StackPanel();health.Children.Add(_healthState);health.Children.Add(_healthUsage);health.Children.Add(_healthAge);
        health.Children.Add(_historyTitle);health.Children.Add(_healthGraph);
        top.Children.Add(new Border { Child=health,Padding=new(8),Margin=new(4),BorderThickness=new(1),BorderBrush=SystemColors.ControlDarkBrush });
        top.Children.Add(_status);top.Children.Add(_items);
        Content=new ScrollViewer { Content=top,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
        _open.Click += async (_, _) => { var dialog = new OpenFileDialog { Filter = "Trusted x64 TrafficMonitor plugin (*.dll)|*.dll" }; if (dialog.ShowDialog(this) == true) await Open(dialog.FileName); };
        _stop.Click += (_, _) => Stop(); _options.Click += async (_, _) => await Poll("options"); _run.Click += async (_, _) => await Poll("command");
        _reload.Click += async (_, _) => { if(_path is {} path) await Open(path); };
        _timer.Tick += async (_, _) => await Poll("sample");
        _healthTimer.Tick += async (_,_)=>await RefreshHealth();
        _language.Changed+=LanguageChanged;
        Closed += (_, _) => { _language.Changed-=LanguageChanged; Stop(); }; LanguageChanged(this,EventArgs.Empty);Controls();
    }
    internal async Task<bool> Open(string path)
    {
        Stop(); _path = path;_failure=null;
        try { _client = _createClient(path);_historyStart=Stopwatch.GetTimestamp();_terminalHealth=TextKey.PluginHealthStarting;RenderHealth();_healthTimer.Start();await Poll("sample");if (_client != null) { _timer.Start();await RefreshHealth(); } return _client != null; }
        catch (Exception e) when (e is InvalidDataException or IOException or System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        { _failure=e;_status.Text = _language[TextKey.PluginFailed] + " " + e.Message; Stop(clearStatus: false,health:TextKey.PluginHealthFailed); return false; }
    }
    private void Controls() { _stop.IsEnabled = _client != null; _open.IsEnabled = !_busy; _reload.IsEnabled = _path != null && !_busy; _options.IsEnabled = _client != null && !_busy; _run.IsEnabled = _client != null && !_busy && _commands.SelectedIndex >= 0; }
    private void Stop(bool clearStatus = true,TextKey health=TextKey.PluginHealthStopped)
    { _timer.Stop();_healthTimer.Stop();_client?.Dispose(); _client = null;_latestHealth=null;_healthHistory.Clear();_healthReadFailed=false;_terminalHealth=health; _publish(null); _items.Children.Clear(); _commands.Items.Clear(); if (clearStatus) {_failure=null;_status.Text = _language[TextKey.BridgeStopped];} RenderHealth();Controls(); }
    private async Task Poll(string operation)
    {
        if (_client == null || _busy) return;
        var client = _client; _busy = true;_operation=operation;RenderHealth(); Controls();
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
            if (ReferenceEquals(client, _client)) { _failure=e;_status.Text = e is PluginResourceLimitException ? _language[TextKey.PluginResourceLimit] : _language[TextKey.PluginFailed] + " " + e.Message; Stop(clearStatus: false,health:e is PluginResourceLimitException?TextKey.PluginHealthLimited:TextKey.PluginHealthFailed); }
        }
        finally { _busy = false;_operation=null;RenderHealth(); Controls(); }
    }
    private async Task RefreshHealth()
    {
        if(_client is not {} client || _healthBusy)return;
        _healthBusy=true;
        try
        {
            var health=await Task.Run(()=>_readHealth(client));
            if(ReferenceEquals(client,_client))
            {
                _latestHealth=health;_healthReadFailed=false;
                bool available=health.Availability==PluginWorkerAvailability.Available;
                _healthHistory.Add(Stopwatch.GetElapsedTime(_historyStart),available?health.CpuPercent:null,available?health.WorkingSetBytes:null);
                RenderHealth();
            }
        }
        catch(Exception e) when(e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {if(ReferenceEquals(client,_client)){_latestHealth=null;_healthReadFailed=true;_healthHistory.Add(Stopwatch.GetElapsedTime(_historyStart),null,null);RenderHealth();}}
        finally{_healthBusy=false;}
    }
    private void RenderHealth()
    {
        var health=_client==null?null:_latestHealth;
        TextKey state=_client==null?_terminalHealth:
            health?.Availability==PluginWorkerAvailability.Exited?TextKey.PluginHealthStopped:
            _operation=="options"?TextKey.PluginHealthSettings:_operation=="command"?TextKey.PluginHealthCommand:
            _healthReadFailed?TextKey.PluginHealthUnavailable:
            health==null?TextKey.PluginHealthStarting:health.Availability==PluginWorkerAvailability.Unavailable?TextKey.PluginHealthUnavailable:
            health.LastResponseAge==null?TextKey.PluginHealthStarting:health.LastResponseAge>TimeSpan.FromSeconds(5)?TextKey.PluginHealthWaiting:TextKey.PluginHealthResponding;
        string unknown=_language[TextKey.NotAvailable];
        bool available=health?.Availability==PluginWorkerAvailability.Available;
        _healthState.Text=_language[TextKey.PluginHealth]+": "+_language[state];
        string cpu=available&&health!.CpuPercent is {} percent?percent.ToString("F1",CultureInfo.InvariantCulture)+"%":unknown;
        string ram=available&&health!.WorkingSetBytes is {} bytes?(bytes/(1024d*1024)).ToString("F1",CultureInfo.InvariantCulture)+" MiB":unknown;
        _healthUsage.Text=_language[TextKey.Cpu]+": "+cpu+"  ·  "+_language[TextKey.Memory]+": "+ram;
        _healthAge.Text=health?.LastResponseAge is {} age?string.Format(CultureInfo.InvariantCulture,_language[TextKey.PluginHealthResponse],age.TotalSeconds.ToString("F0",CultureInfo.InvariantCulture)):_language[_client==null||_healthReadFailed?TextKey.PluginHealthResponseUnknown:TextKey.PluginHealthNoResponse];
        string allocated=available&&health!.PrivateBytes is {} privateBytes?(privateBytes/(1024d*1024)).ToString("F1",CultureInfo.InvariantCulture):unknown;
        _healthUsage.ToolTip=string.Format(CultureInfo.InvariantCulture,_language[TextKey.PluginHealthHint],allocated,health?.Uptime.TotalSeconds.ToString("F0",CultureInfo.InvariantCulture)??unknown);
        AutomationProperties.SetName(_healthState,_healthState.Text);
        AutomationProperties.SetName(_healthUsage,_healthUsage.Text);
        _historyTitle.Text=_language[TextKey.PluginHistory];
        _healthGraph.Update(_healthHistory.Samples,_language,_healthUsage.Text);
    }
    private void LanguageChanged(object? sender,EventArgs args)
    {
        Title=_language[TextKey.PluginBridge];_hint.Text=_language[TextKey.PluginHint];
        _open.Content=_language[TextKey.PluginChoose];_stop.Content=_language[TextKey.BridgeStop];_options.Content=_language[TextKey.PluginOptions];_run.Content=_language[TextKey.PluginRun];_reload.Content=_language[TextKey.PluginReload];
        AutomationProperties.SetName(_healthState,_language[TextKey.PluginHealth]);AutomationProperties.SetLiveSetting(_healthState,AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_healthUsage,_language[TextKey.Cpu]+" / "+_language[TextKey.Memory]);
        if(_failure!=null)_status.Text=_failure is PluginResourceLimitException?_language[TextKey.PluginResourceLimit]:_language[TextKey.PluginFailed]+" "+_failure.Message;
        else if(_client==null&&_terminalHealth==TextKey.PluginHealthStopped)_status.Text=_language[TextKey.BridgeStopped];
        RenderHealth();
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
