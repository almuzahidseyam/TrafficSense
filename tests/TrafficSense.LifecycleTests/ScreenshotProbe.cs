using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TrafficSense;

// Reproducible screenshots of real controls with explicitly illustrative data.
internal static class ScreenshotProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static int Run(string output)
    {
        Directory.CreateDirectory(output);
        var settings = new AppSettings { WidgetFontSize = 18, WidgetBackground = "#061C2D", BackgroundOpacity = 255,
            DownloadColor = "#45E0B6", UploadColor = "#60A5FA", MetricsColor = "#F1F5F9", DisplayMetrics = (int)(SystemMetric.Cpu | SystemMetric.Memory) };
        long received = 100, sent = 100; double seconds = 0;
        var now = new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.FromHours(6));
        string temporary = Path.Combine(Path.GetTempPath(), "TrafficSense.Demo", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporary);
        var usage = new UsageStore(Path.Combine(temporary, "usage.json"));
        usage.Save(new UsageLedger(Enumerable.Range(0, 13).Select(day => new UsageDay(new(2026, 10, day + 1),
            (day + 1) * 160_000_000L + day % 3 * 300_000_000L, (day + 1) * 35_000_000L))));
        var app = new App(@"Local\TrafficSense.Demo." + Guid.NewGuid().ToString("N"), () => new MainWindow(settings, _ => new(true),
            readAdapters: () => [new("demo", "Illustrative adapter", "Demo", NetworkInterfaceType.Ethernet, true, true, true, new("demo", received, sent))],
            sampleTimestamp: () => TimeSpan.FromSeconds(seconds), usageStore: usage, localTime: () => now,
            readSystemMetrics: _ => new() { CpuPercent = 12.4, TotalMemory = 16UL * 1024 * 1024 * 1024, AvailableMemory = 10UL * 1024 * 1024 * 1024 }));
        app.InitializeComponent();
        foreach (var type in new[] { typeof(MainWindow), typeof(HistoryWindow) })
        {
            var style = new Style(type); style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
            style.Setters.Add(new Setter(Window.ShowActivatedProperty, false)); app.Resources.Add(type, style);
        }
        int result = 1;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var window = (MainWindow)app.MainWindow;
                ((DispatcherTimer)typeof(MainWindow).GetField("_timer", Private)!.GetValue(window)!).Stop();
                var service = (SystemMetricsService)typeof(MainWindow).GetField("_systemService", Private)!.GetValue(window)!;
                if (!SpinWait.SpinUntil(() => service.Latest.CpuPercent.HasValue, 3000)) throw new InvalidOperationException("Demo metrics unavailable.");
                for (int i = 0; i < 120; i++)
                {
                    seconds++; received += (long)((6 + 2 * Math.Sin(i / 10d)) * 1024 * 1024); sent += 384 * 1024;
                    typeof(MainWindow).GetMethod("Timer_Tick", Private)!.Invoke(window, [null, EventArgs.Empty]);
                }
                Capture(window, Path.Combine(output, "widget-en.png"));
                window.ChangeLanguage("bn"); Capture(window, Path.Combine(output, "widget-bn.png"));
                window.ChangeLanguage("en"); window.ShowHistory();
                var history = (HistoryWindow)typeof(MainWindow).GetField("_historyWindow", Private)!.GetValue(window)!;
                history.SizeToContent = SizeToContent.Manual; history.Width = 620; history.Height = 920;
                Capture(history, Path.Combine(output, "history-en.png"));
                string? example = Environment.GetEnvironmentVariable("TRAFFICSENSE_EXAMPLE_SKIN");
                if (example != null) { window.ChangeLayoutSkin(example,false,Path.Combine(temporary,"skins")); Capture(window,Path.Combine(output,"layout-mint.png")); }
                window.Close(); result = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); app.Shutdown(1); }
        }));
        app.Run();
        if (result != 0) throw new InvalidOperationException("Screenshot generation failed.");
        Console.WriteLine("Saved actual WPF controls with illustrative counters; user desktop/settings/clipboard untouched."); return 0;
    }
    private static void Capture(Window window, string path)
    {
        window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        var root = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * 2), (int)Math.Ceiling(root.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
