using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace TrafficSense;

// Runs in its own process through --sensor-bridge; an unavailable sensor
// provider never shares the network monitor's dispatcher or native drivers.
internal sealed class SensorBridgeWindow : Window
{
    private readonly UiLanguage _language;
    private readonly string _output;
    private readonly HttpClient _client;
    private readonly TextBox _port = new() { Text = "8085", MaxLength = 5, Width = 100, HorizontalAlignment = HorizontalAlignment.Left, Style = null };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 12) };
    private readonly Button _start = new() { Style = null, Margin = new(0, 12, 12, 0), Padding = new(12, 4, 12, 4) };
    private readonly Button _stop = new() { Style = null, Margin = new(0, 12, 0, 0), Padding = new(12, 4, 12, 4), IsEnabled = false };
    private CancellationTokenSource? _cancellation;
    private bool _closed;

    internal SensorBridgeWindow(UiLanguage language, string? output = null, HttpMessageHandler? handler = null)
    {
        _language = language; _output = output ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrafficSense", "sensors.json");
        _client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
        { Timeout = TimeSpan.FromSeconds(2), MaxResponseContentBufferSize = 1024 * 1024 };
        Title = language[TextKey.SensorBridge]; Width = 520; MinWidth = 360; Height = 430;
        Background = SystemColors.WindowBrush; Foreground = SystemColors.WindowTextBrush; FontSize = 14;
        FontFamily = new FontFamily("Segoe UI, Nirmala UI"); WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = language[TextKey.BridgeHint], TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = language[TextKey.SensorPort], Margin = new(0, 12, 0, 4) });
        AutomationProperties.SetName(_port, language[TextKey.SensorPort]); panel.Children.Add(_port);
        _start.Content = language[TextKey.BridgeStart]; _stop.Content = language[TextKey.BridgeStop];
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; actions.Children.Add(_start); actions.Children.Add(_stop); panel.Children.Add(actions);
        panel.Children.Add(_status); panel.Children.Add(new TextBlock { Text = language[TextKey.SensorOutput] + "\n" + _output, TextWrapping = TextWrapping.Wrap });
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        _start.Click += async (_, _) => await StartAsync(); _stop.Click += (_, _) => Stop();
        Closed += (_, _) => { _closed = true; Stop(); _client.Dispose(); };
    }

    internal async Task StartAsync()
    {
        if (_closed || _cancellation != null) return;
        if (!int.TryParse(_port.Text, out int port) || port is < 1 or > 65535) { SetStatus(_language[TextKey.BridgeInvalidPort]); return; }
        var cancellation = _cancellation = new CancellationTokenSource();
        _start.IsEnabled = _port.IsEnabled = false; _stop.IsEnabled = true;
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    var sample = await ReadOnceAsync(port, cancellation.Token);
                    if (_closed || cancellation.IsCancellationRequested) break;
                    SetStatus(string.Format(_language[TextKey.BridgeConnected], sample.Sensors.Count));
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { break; }
                catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException or IOException or
                    UnauthorizedAccessException or InvalidOperationException or ArgumentException)
                { if (!_closed) SetStatus(_language[TextKey.BridgeFailed]); }
                await Task.Delay(1000, cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(_cancellation, cancellation)) _cancellation = null;
            cancellation.Dispose();
            if (!_closed) { _start.IsEnabled = _port.IsEnabled = true; _stop.IsEnabled = false; }
        }
    }

    internal async Task<ExternalSensorSample> ReadOnceAsync(int port, CancellationToken cancellation)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        byte[] data = await _client.GetByteArrayAsync(new Uri($"http://127.0.0.1:{port}/data.json"), cancellation).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        var sample = LibreSensorBridge.Parse(data, DateTimeOffset.UtcNow);
        LibreSensorBridge.Write(_output, sample); return sample;
    }
    internal void Stop() { _cancellation?.Cancel(); if (!_closed) SetStatus(_language[TextKey.BridgeStopped]); }
    private void SetStatus(string text) { _status.Text = text; AutomationProperties.SetName(_status, text); }
}
