using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TrafficSense;

internal static class SensorBridgeProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Fixture = """
        {"Children":[{"HardwareId":"/cpu/0","Text":"CPU","Children":[
        {"Type":"Temperature","SensorId":"/cpu/0/temperature/0","Text":"Package","RawValue":42.5}]}]}
        """;
    public static int VerifyEntry()
    {
        var app = new App(@"Local\TrafficSense.BridgeEntry." + Guid.NewGuid().ToString("N")); app.InitializeComponent();
        var style = new Style(typeof(SensorBridgeWindow)); style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
        style.Setters.Add(new Setter(Window.ShowActivatedProperty, false)); app.Resources.Add(typeof(SensorBridgeWindow), style);
        bool passed = false;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            Check(app.MainWindow is SensorBridgeWindow && app.Windows.Count == 1 && typeof(App).GetField("_tray", Private)!.GetValue(app) == null,
                "Sensor mode started a network widget/tray instead of its separate window.");
            passed = true; app.MainWindow.Close();
        }));
        int exit = app.Run(); Check(exit == 0 && passed, "Sensor bridge entry mode failed.");
        Console.WriteLine("PASS: production --sensor-bridge startup branch with isolated instance, hidden window, no network widget/tray and clean Exit."); return 0;
    }
    public static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "TrafficSense.BridgeProbe", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        string path = Path.Combine(root, "sensors.json"); var handler = new Provider();
        var app = new App(@"Local\TrafficSense.BridgeProbe." + Guid.NewGuid().ToString("N"), () => new MainWindow(new(), _ => new(true), readAdapters: () => []));
        app.InitializeComponent();
        foreach (var type in new[] { typeof(MainWindow), typeof(SensorBridgeWindow) })
        {
            var style = new Style(type); style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
            style.Setters.Add(new Setter(Window.ShowActivatedProperty, false)); app.Resources.Add(type, style);
        }
        int result = 1;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(async () =>
        {
            SensorBridgeWindow? window = null;
            try
            {
                window = new(new UiLanguage("bn"), path, handler); window.Show();
                var sample = await window.ReadOnceAsync(8085, CancellationToken.None);
                Check(sample.Sensors.Single().Value == 42.5 && ExternalSensorFile.Read(path, DateTimeOffset.UtcNow)?.Sensors.Single().Unit == "°C",
                    "Bridge did not write a fresh exact sensor sample.");
                Check(handler.LastUri?.Host == "127.0.0.1" && handler.LastUri.AbsolutePath == "/data.json", "Bridge requested a non-local source.");
                string previous = File.ReadAllText(path); handler.Body = "{}";
                try { await window.ReadOnceAsync(8085, CancellationToken.None); throw new Exception("Invalid provider was accepted."); }
                catch (System.Text.Json.JsonException) { }
                Check(File.ReadAllText(path) == previous, "Failed provider replaced the previous sample with fake data.");
                handler.Body = Fixture;
                var port = (TextBox)typeof(SensorBridgeWindow).GetField("_port", Private)!.GetValue(window)!;
                port.Text = "0"; await window.StartAsync();
                var status = (TextBlock)typeof(SensorBridgeWindow).GetField("_status", Private)!.GetValue(window)!;
                Check(status.Text.Contains("৬৫৫৩৫"), "Invalid port was not explained in Bengali.");
                port.Text = "8085"; var running = window.StartAsync();
                await Task.Delay(100); window.Stop(); await running;
                Check(status.Text.Contains("বিচ্ছিন্ন"), "Disconnect did not cancel polling.");
                handler.Block = true; running = window.StartAsync(); await Task.Delay(100); window.Close(); await running;
                Check(Directory.GetFiles(root, "*.tmp").Length == 0, "Bridge left an atomic-write temporary file.");
                result = 0; app.MainWindow.Close();
            }
            catch (Exception error) { Console.Error.WriteLine(error); window?.Close(); app.Shutdown(1); }
        }));
        int exit = app.Run(); Check(result == 0 && exit == 0, "Bridge probe failed.");
        Console.WriteLine("PASS: separate sensor bridge UI, loopback-only bounded provider request, numeric temperature/atomic file, failed provider preservation, translated invalid port, disconnect and close cancellation. No hardware driver or real sensor server was started.");
        return 0;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Provider : HttpMessageHandler
    {
        public string Body = Fixture; public bool Block; public Uri? LastUri;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            if (Block) await Task.Delay(Timeout.Infinite, cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
        }
    }
}
