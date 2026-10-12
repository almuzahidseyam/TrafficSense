using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using TrafficSense;

// Exercise the production WPF message hook and worker-thread network handler
// with deterministic counters/time. Never sleep the PC or change a real NIC.
internal static class NetworkRecoveryProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static int Run()
    {
        IReadOnlyCollection<NetworkAdapterInfo> adapters = [Adapter("wifi", 1000, 500)];
        TimeSpan time = TimeSpan.Zero;
        var settings = new AppSettings { SelectedAdapterId = "wifi", Language = "bn" };
        var app = new App(@"Local\TrafficSense.Recovery." + Guid.NewGuid().ToString("N"),
            () => new MainWindow(settings, _ => new SettingsSaveResult(true),
                message => throw new InvalidOperationException(message), () => adapters, () => time));
        app.InitializeComponent();
        var style = new Style(typeof(MainWindow));
        style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
        style.Setters.Add(new Setter(Window.ShowActivatedProperty, false));
        app.Resources.Add(typeof(MainWindow), style);

        int result = 1;
        MainWindow? window = null;
        DispatcherTimer? timer = null;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            window = (MainWindow)app.MainWindow;
            try
            {
                timer = (DispatcherTimer)Field(window, "_timer")!;
                timer.Stop(); // Deterministic samples, driven through the real Tick handler.
                var download = (TextBlock)window.FindName("TxtDownload");
                var upload = (TextBlock)window.FindName("TxtUpload");
                IntPtr handle = new WindowInteropHelper(window).Handle;

                void Sample(long received, long sent, double seconds, NetworkSpeed expected)
                {
                    adapters = [Adapter("wifi", received, sent)];
                    time = TimeSpan.FromSeconds(seconds);
                    Tick(window);
                    Check((NetworkSpeed)Field(window, "_latestSpeed")! == expected, "Unexpected speed after recovery.");
                    if (window.IsVisible)
                        Check(download.Text == SpeedFormatter.Format(expected.DownloadBytesPerSecond)
                            && upload.Text == SpeedFormatter.Format(expected.UploadBytesPerSecond), "Displayed speed is stale.");
                }

                Sample(3000, 1000, 1, new(2000, 500));
                Power(handle, 0x000A); // Battery/AC status alone must not reset throughput.
                Sample(5000, 1500, 2, new(2000, 500));
                Power(handle, 0x0004); // Suspend clears the currently displayed speed.
                Check(download.Text == SpeedFormatter.Format(0) && upload.Text == SpeedFormatter.Format(0), "Suspend left stale speed.");
                Power(handle, 0x0012); // Automatic resume, including unattended wake.
                Sample(9_000_000, 6_000_000, 602, default);
                Sample(9_002_000, 6_000_500, 603, new(2000, 500));
                Power(handle, 0x0007); // User-interactive resume may follow automatic resume.
                Sample(9_500_000, 6_500_000, 604, default);
                Sample(9_502_000, 6_500_500, 605, new(2000, 500));

                // A rapid down/up can occur entirely between one-second reads,
                // with the same ID and monotonically increasing byte counters.
                NotifyFromWorker(window, 200);
                Sample(20_000_000, 10_000_000, 606, default);
                Sample(20_002_000, 10_000_500, 607, new(2000, 500));

                adapters = []; // Explicit choice remains selected while offline.
                time = TimeSpan.FromSeconds(608);
                Tick(window);
                Check((NetworkSpeed)Field(window, "_latestSpeed")! == default, "Offline speed was not zero.");
                adapters = [Adapter("ethernet", 50_000_000, 30_000_000)];
                time = TimeSpan.FromSeconds(609);
                Tick(window);
                Check(Field(window, "_monitoredAdapter") == null && settings.SelectedAdapterId == "wifi", "Manual choice silently switched adapters.");
                Sample(21_000_000, 11_000_000, 610, default);
                Sample(21_002_000, 11_000_500, 611, new(2000, 500));

                // Missing power notifications still have a sampling-gap fallback.
                Sample(40_000_000, 20_000_000, 1200, default);
                Sample(40_002_000, 20_000_500, 1201, new(2000, 500));

                window.Hide();
                download.Text = "hidden recovery sentinel";
                Power(handle, 0x0004);
                Power(handle, 0x0012);
                NotifyFromWorker(window, 200);
                Sample(50_000_000, 30_000_000, 1202, default);
                Sample(50_002_000, 30_000_500, 1203, new(2000, 500));
                Check(!window.IsVisible && download.Text == "hidden recovery sentinel", "Hidden recovery changed visibility or text/layout.");
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
                Check(download.Text == SpeedFormatter.Format(2000), "Showing after recovery did not render the latest speed.");
                Check(settings.SelectedAdapterId == "wifi" && window.Localization.Code == "bn", "Recovery changed preferences.");

                // Auto follows a new preferred route without summing the VPN and Wi-Fi.
                settings.SelectedAdapterId = null;
                adapters = [Adapter("wifi", 50_004_000, 30_001_000), Adapter("vpn", 70_000_000, 40_000_000) with { IsPreferredRoute = true }];
                time = TimeSpan.FromSeconds(1204);
                NotifyFromWorker(window, 2);
                Tick(window);
                Check(((NetworkAdapterInfo)Field(window, "_monitoredAdapter")!).Id == "vpn"
                    && (NetworkSpeed)Field(window, "_latestSpeed")! == default, "Auto route did not reset on VPN reconnect.");
                adapters = [Adapter("wifi", 50_104_000, 30_101_000), Adapter("vpn", 70_002_000, 40_000_500) with { IsPreferredRoute = true }];
                time = TimeSpan.FromSeconds(1205);
                Tick(window);
                Check((NetworkSpeed)Field(window, "_latestSpeed")! == new NetworkSpeed(2000, 500), "Recovery double-counted VPN and Wi-Fi.");

                // A stalled background provider keeps its delivery timestamp unchanged.
                // The UI must still add an unknown point rather than retaining a fresh legend.
                var sampler = new NetworkSamplingService(() => adapters, () => time, () => DateTimeOffset.Now, null, 1000, start: false);
                typeof(MainWindow).GetField("_sampler", Private)!.SetValue(window, sampler);
                time = TimeSpan.FromSeconds(1206); sampler.Sample(); Tick(window);
                adapters = [Adapter("vpn", 70_004_000, 40_001_000) with { IsPreferredRoute = true }];
                time = TimeSpan.FromSeconds(1207); sampler.Sample(); Tick(window);
                var history = (SpeedHistory)Field(window, "_speedHistory")!;
                Check(history.Samples.Last().IsValid, "Background sample was not fresh.");
                time = TimeSpan.FromSeconds(1213); Tick(window);
                Check(!history.Samples.Last().IsValid && history.Samples.Last().Timestamp == time,
                    "Stalled background delivery retained a fresh history value.");

                timer.Start();
                result = 0;
                window.Close();
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                app.Shutdown(1);
            }
        }));
        int exit = app.Run();
        Check(result == 0 && exit == 0 && timer is { IsEnabled: false }, "Recovery probe failed or timer survived Exit.");
        Check((int)Field(window!, "_monitoringClosed")! == 1, "Recovery subscriptions were not closed.");
        NotifyFromWorker(window!, 200);
        Check((int)Field(window!, "_baselineResetPending")! == 0, "Late network callback affected a closed window.");
        Console.WriteLine("PASS: simulated suspend/automatic and interactive resume, short reconnect between ticks, offline/manual wait, long sampling gap, hidden recovery, worker notification bursts/late callbacks, Auto VPN route and Exit cleanup.");
        return 0;
    }

    private static NetworkAdapterInfo Adapter(string id, long received, long sent) =>
        new(id, id, id, NetworkInterfaceType.Wireless80211, true, true, true, new(id, received, sent));

    private static object? Field(MainWindow window, string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
    private static void Tick(MainWindow window) => typeof(MainWindow).GetMethod("Timer_Tick", Private)!.Invoke(window, [null, EventArgs.Empty]);
    private static void NotifyFromWorker(MainWindow window, int count) => Task.Run(() =>
    {
        var callback = typeof(MainWindow).GetMethod("NetworkChanged", Private)!;
        for (int index = 0; index < count; index++) callback.Invoke(window, [null, EventArgs.Empty]);
    }).GetAwaiter().GetResult();
    private static void Power(IntPtr handle, int kind) => SendMessage(handle, 0x0218, new IntPtr(kind), IntPtr.Zero);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
