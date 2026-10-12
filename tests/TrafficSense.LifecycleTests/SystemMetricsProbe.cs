using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TrafficSense;

internal static class SystemMetricsProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static int Run()
    {
        var settings = new AppSettings(); bool failSave = false; int reads = 0;
        var fake = new SystemMetrics(25, 8UL << 30, 6UL << 30, 2400, 60, 30, 1024, 2048);
        var errors = new List<string>();
        var app = new App(@"Local\TrafficSense.Metrics." + Guid.NewGuid().ToString("N"), () =>
            new MainWindow(settings, _ => new(!failSave, failSave ? "blocked" : null), errors.Add, readAdapters: () => [],
                readSystemMetrics: _ => { Interlocked.Increment(ref reads); return fake; }));
        app.InitializeComponent();
        foreach (var type in new[] { typeof(MainWindow), typeof(SystemDetailsWindow) })
        {
            var style = new Style(type); style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
            style.Setters.Add(new Setter(Window.ShowActivatedProperty, false)); app.Resources.Add(type, style);
        }
        int result = 1;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            var window = (MainWindow)app.MainWindow;
            try
            {
                Check(Field<SystemMetricsService?>(window, "_systemService") == null && reads == 0, "Disabled metrics started polling.");
                Check(window.ChangeDisplayedMetrics(SystemMetric.All), "Metrics save failed.");
                Check(SpinWait.SpinUntil(() => Volatile.Read(ref reads) > 0, TimeSpan.FromSeconds(3)), "Metrics worker did not run.");
                Refresh(window);
                var text = (TextBlock)window.FindName("TxtMetrics");
                Check(text.Text.Contains("CPU: 25.0%") && text.Text.Contains("RAM: 25.0%") && text.Text.Contains("2.4 GHz") && text.Text.Contains("60.0%") && text.IsVisible,
                    "Optional metrics values or visibility incorrect.");
                window.ChangeLanguage("bn"); Check(text.Text.Contains("CPU ব্যবহার"), "Metric language did not change.");
                failSave = true; Check(!window.ChangeDisplayedMetrics(SystemMetric.None) && settings.DisplayMetrics == 31 && errors.Count == 1, "Failed metric save did not roll back.");
                failSave = false;
                window.ShowSystemDetails(); var details = Field<Window>(window, "_systemWindow");
                Check(details.Title == "সিস্টেম ও সংযোগের বিস্তারিত", "Details title untranslated.");
                window.Hide(); text.Text = "hidden sentinel"; Refresh(window); Check(text.Text == "hidden sentinel", "Hidden widget rendered metrics.");
                details.Close(); window.ChangeDisplayedMetrics(SystemMetric.None);
                Check(Field<SystemMetricsService?>(window, "_systemService") == null, "Disabling all metrics retained polling.");
                using (var native = new WindowsSystemMetricsReader(SystemMetric.All))
                {
                    var measured = native.Read();
                    Thread.Sleep(1050); measured = native.Read();
                    Console.WriteLine("Native optional counters: " + System.Text.Json.JsonSerializer.Serialize(measured));
                    Check(measured.TotalMemory > 0 && measured.AvailableMemory <= measured.TotalMemory && measured.MemoryPercent is >= 0 and <= 100,
                        "Native physical memory layout/read invalid.");
                    // Unsupported provider counters are allowed; fabricated values are not.
                    foreach (double? percent in new[] { measured.CpuPercent, measured.GpuPercent, measured.DiskPercent })
                        Check(percent == null || percent is >= 0 and <= 100, "Native utilization outside range.");
                }
                window.ShowSystemDetails(); window.Close();
                Check(Field<Window?>(window, "_systemWindow") == null, "Exit retained system details.");
                result = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); app.Shutdown(1); }
        }));
        int exit = app.Run(); Check(exit == 0 && result == 0, "System metrics probe failed.");
        Console.WriteLine("PASS: optional native CPU/RAM/frequency/GPU/disk counters, bilingual widget/details, no polling when disabled, save rollback, hidden rendering guard and Exit cleanup.");
        return 0;
    }
    private static T Field<T>(MainWindow window, string name) => (T)typeof(MainWindow).GetField(name, Private)!.GetValue(window)!;
    private static void Refresh(MainWindow window) => typeof(MainWindow).GetMethod("RefreshSystemMetrics", Private)!.Invoke(window, null);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
