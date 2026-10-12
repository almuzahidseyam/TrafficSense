using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TrafficSense;

internal static class CustomizationProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "TrafficSense.Customization", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var store = new SettingsStore(Path.Combine(root, "settings.json")); var usageStore = new UsageStore(Path.Combine(root, "usage.json"));
        var now = new DateTimeOffset(2026, 10, 13, 1, 0, 0, TimeSpan.FromHours(6)); var date = DateOnly.FromDateTime(now.Date);
        usageStore.Save(new UsageLedger([new(date, 1024L * 1024 * 1024 - 100, 0)]));
        bool fail = false; long received = 1000; double time = 0; var alerts = new List<UsageAlertNotification>(); var errors = new List<string>();
        var app = new App(@"Local\TrafficSense.Customization." + Guid.NewGuid().ToString("N"), () => new MainWindow(new(),
            settings => fail ? new(false, "blocked") : store.Save(settings), errors.Add,
            readAdapters: () => [new("wifi", "Wi-Fi", "Wi-Fi", System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211, true, true, true, new("wifi", received, 0))],
            sampleTimestamp: () => TimeSpan.FromSeconds(time), usageStore: usageStore, localTime: () => now));
        app.InitializeComponent(); var style = new Style(typeof(MainWindow)); style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
        style.Setters.Add(new Setter(Window.ShowActivatedProperty, false)); app.Resources.Add(typeof(MainWindow), style);
        int result = 1;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            var window = (MainWindow)app.MainWindow;
            window.UsageAlert += alerts.Add;
            try
            {
                ((DispatcherTimer)typeof(MainWindow).GetField("_timer", Private)!.GetValue(window)!).Stop();
                Check(window.ChangeUsageLimits(1, 1) && alerts.Count == 0, "Configuring alerts notified old totals.");
                received += 150; time++; Tick(window); Check(alerts.Count == 2 && alerts.All(alert => alert.Message.Contains("1.00 GiB")), "Exact combined crossing alerts missing.");
                received += 150; time++; Tick(window); Check(alerts.Count == 2, "Alerts repeated above threshold.");
                window.UsageAlert += _ => throw new System.ComponentModel.Win32Exception("Unavailable test notification shell");
                // The normal tray event path must also tolerate shell failure without losing a sample.
                Check(window.ChangeUsageLimits(5, 5), "Higher limits not saved.");
                received += 5L * 1024 * 1024 * 1024; time++; Tick(window);
                Check(window.ChangeUsageLimits(1, 1), "Limits not restored after notification failure.");
                var skin = new WidgetSkin(18, "Consolas, Nirmala UI", false, 191, "#061C2D", "#45E0B6", "#60A5FA", "#FFFFFF");
                string path = Path.Combine(root, "skin.json"); skin.Export(path); Check(window.ImportSkin(path), "Skin import failed."); Drain();
                var download = (TextBlock)window.FindName("TxtDownload");
                Check(download.FontSize == 18 && download.FontFamily.Source == skin.FontFamily && download.FontWeight == FontWeights.Normal &&
                    ((SolidColorBrush)download.Foreground).Color == (Color)ColorConverter.ConvertFromString("#45E0B6"), "Imported appearance not rendered.");
                Check(store.Load().DailyLimitGiB == 1 && store.Load().SelectedAdapterId == null, "Skin changed unrelated settings.");
                string saved = File.ReadAllText(Path.Combine(root, "settings.json"));
                File.WriteAllText(path, "{}"); Check(!window.ImportSkin(path) && saved == File.ReadAllText(Path.Combine(root, "settings.json")), "Invalid skin changed settings.");
                File.WriteAllText(path, "{\"Format\":\"TrafficSense.Skin\",\"Version\":1e100}");
                Check(!window.ImportSkin(path) && saved == File.ReadAllText(Path.Combine(root, "settings.json")), "Extreme version crashed or changed settings.");
                skin.Export(path); fail = true; Check(!window.ApplySkin(skin with { FontSize = 24 }) && download.FontSize == 18, "Failed skin save changed appearance.");
                Check(!window.ChangeUsageLimits(5, 5) && store.Load().DailyLimitGiB == 1, "Failed alert save changed limit."); fail = false;
                window.ChangeLanguage("bn"); Check(((MenuItem)window.FindName("MenuSkinPresets")).Header.ToString() == "দেখানোর পূর্বনির্ধারিত স্টাইল", "Customization not translated.");
                string sensor = Path.Combine(root, "sensors.json");
                File.WriteAllText(sensor, JsonSerializer.Serialize(new { Format = "TrafficSense.Sensors", Version = 1, UpdatedAt = DateTimeOffset.UtcNow,
                    Sensors = new[] { new ExternalSensor("cpu", "CPU package", 42.5, "°C") } }));
                Check(window.ChangeSensorSource(sensor) && window.ChangeDisplayedMetrics(SystemMetric.Sensors), "Sensor connection failed.");
                var service = (SystemMetricsService)typeof(MainWindow).GetField("_systemService", Private)!.GetValue(window)!;
                Check(SpinWait.SpinUntil(() => service.Latest.External != null, TimeSpan.FromSeconds(3)), "Sensor worker did not publish file.");
                Tick(window); Check(((TextBlock)window.FindName("TxtMetrics")).Text.Contains("42.5 °C"), "Sensor reading missing.");
                Check(!window.ChangeSensorSource("https://example.com/sensors.json") && store.Load().SensorFile == sensor, "Invalid sensor path changed configured source.");
                Check(window.ChangeSensorSource(null), "Sensor disconnect failed."); Tick(window);
                Check(((TextBlock)window.FindName("TxtMetrics")).Text.Contains("নতুন sensor তথ্য নেই"), "Disconnected sensor displayed fake zero/stale data.");
                window.Close(); result = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); app.Shutdown(1); }
        }));
        int exit = app.Run(); Check(result == 0 && exit == 0, "Customization probe failed.");
        Console.WriteLine("PASS: native skin/font/color application, bilingual controls, malformed/save rollback, daily/monthly crossing alerts through an isolated sink, optional fresh sensor file/disconnect and Exit.");
        return 0;
    }
    private static void Tick(MainWindow window) => typeof(MainWindow).GetMethod("Timer_Tick", Private)!.Invoke(window, [null, EventArgs.Empty]);
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
