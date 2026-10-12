using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TrafficSense;

internal static class FeatureProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static int Run()
    {
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TrafficSense.FeatureTests"));
        string root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(Path.Combine(root, "settings.json"));
        var usageStore = new UsageStore(Path.Combine(root, "usage.json"));
        Check(store.Save(new AppSettings()).Success, "Could not prepare settings.");
        double seconds = 0;
        long received = 10000, sent = 20000;
        bool permitShortcut = false;
        int registers = 0, unregisters = 0;
        var shortcut = new GlobalShortcut(_ => { registers++; return permitShortcut; }, _ => unregisters++);
        var errors = new List<string>();
        var app = new App(@"Local\TrafficSense.Features." + Guid.NewGuid().ToString("N"), () =>
            new MainWindow(store.Load(), store.Save, errors.Add,
                readAdapters: () => [new("test", "Test", "Isolated adapter", NetworkInterfaceType.Ethernet, true, true, true,
                    new NetworkAdapterSnapshot("test", received, sent))], sampleTimestamp: () => TimeSpan.FromSeconds(seconds),
                usageStore: usageStore, localTime: () => new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.FromHours(6)), shortcut: shortcut));
        app.InitializeComponent();
        foreach (var type in new[] { typeof(MainWindow), typeof(HistoryWindow), typeof(AboutWindow) })
        {
            var style = new Style(type);
            style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
            style.Setters.Add(new Setter(Window.ShowActivatedProperty, false));
            app.Resources.Add(type, style);
        }
        bool passed = false;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var window = (MainWindow)app.MainWindow;
                Drain(window);
                var refresh = typeof(MainWindow).GetMethod("RefreshNetworkSpeed", Private)!;
                for (int i = 0; i < 10; i++) { seconds++; received += 125000; sent += 62500; refresh.Invoke(window, null); }
                var usage = (UsageLedger)typeof(MainWindow).GetField("_usage", Private)!.GetValue(window)!;
                Check(usage.Today(new(2026, 10, 12)).Received == 1250000 && usage.Month(new(2026, 10, 12)).Sent == 625000,
                    "Live traffic totals differ from sampled counter deltas.");
                var unit = ((MenuItem)window.FindName("MenuSpeedUnit")).Items.OfType<MenuItem>().Single(i => (bool)i.Tag);
                Click(unit);
                Check(((TextBlock)window.FindName("TxtDownload")).Text.EndsWith("Mbps") && store.Load().SpeedInBits && unit.IsChecked,
                    "Routed speed-unit click did not render or save bits.");
                Click(((MenuItem)window.FindName("MenuRefreshInterval")).Items.OfType<MenuItem>().Single(i => (int)i.Tag == 500));
                seconds += 0.5; received += 62500; sent += 31250; refresh.Invoke(window, null);
                Check(((TextBlock)window.FindName("TxtDownload")).Text == SpeedFormatter.Format(125000, true),
                    "Changed interval stopped using actual elapsed time.");
                Check(!window.ChangeGlobalShortcut(true) && !shortcut.Enabled && !store.Load().GlobalShortcutEnabled,
                    "Shortcut conflict was not handled.");
                permitShortcut = true;
                using (var locked = new FileStream(Path.Combine(root, "settings.json"), FileMode.Open, FileAccess.Read, FileShare.None))
                    Check(!window.ChangeGlobalShortcut(true), "Blocked shortcut save succeeded.");
                Check(!shortcut.Enabled && unregisters == 1, "Failed save retained shortcut registration.");
                Check(window.ChangeGlobalShortcut(true) && shortcut.Enabled && store.Load().GlobalShortcutEnabled, "Shortcut did not save.");
                SendMessage(new WindowInteropHelper(window).Handle, 0x0312, new IntPtr(GlobalShortcut.Id), IntPtr.Zero);
                Check(!window.IsVisible, "Global shortcut message did not hide widget.");
                window.ShowHistory();
                var history = (HistoryWindow)typeof(MainWindow).GetField("_historyWindow", Private)!.GetValue(window)!;
                Drain(history);
                Check(!window.IsVisible && history.IsVisible, "History changed widget visibility.");
                window.ShowHistory();
                Check(ReferenceEquals(history, typeof(MainWindow).GetField("_historyWindow", Private)!.GetValue(window)), "Repeated history opened duplicates.");
                var graph = (SpeedGraph)typeof(HistoryWindow).GetField("_graph", Private)!.GetValue(history)!;
                Check(graph.ActualWidth > 100 && AutomationProperties.GetName(graph).Contains("Mbps"), "Graph did not size or describe its units.");
                window.ChangeLanguage("bn");
                Check(history.Title == "ডেটা ব্যবহার ও ইতিহাস" && AutomationProperties.GetName(graph).Contains("ডাউনলোড"), "History did not translate.");
                Render(history);
                history.Close();
                Check(typeof(MainWindow).GetField("_historyWindow", Private)!.GetValue(window) == null, "History was not released.");
                window.Show(); Drain(window);
                window.Left = 100; window.Top = 100;
                Check(window.HandleWidgetKey(Key.Right, ModifierKeys.None) && window.Left == 110, "Keyboard movement failed.");
                Check(window.HandleWidgetKey(Key.L, ModifierKeys.Control) && window.PositionLocked, "Keyboard lock failed.");
                double left = window.Left;
                Check(!window.HandleWidgetKey(Key.Right, ModifierKeys.None) && window.Left == left, "Keyboard movement bypassed lock.");
                Check(window.HandleWidgetKey(Key.R, ModifierKeys.Control) && window.PositionLocked, "Keyboard reset changed lock.");
                Check(window.HandleWidgetKey(Key.H, ModifierKeys.Control), "Keyboard history failed.");
                Check(AutomationProperties.GetName((TextBlock)window.FindName("TxtDownload")) == "ডাউনলোডের গতি", "Speed lacks a localized accessible name.");
                window.Close();
                Check(!shortcut.Enabled && registers == 3 && unregisters == 2 && errors.Count == 2, "Shortcut cleanup/errors are incorrect.");
                Check(usageStore.Load().Today(new(2026, 10, 12)).Received == 1312500, "Exit did not persist usage.");
                Check(app.Windows.OfType<HistoryWindow>().Count() == 0, "Exit left history open.");
                passed = true;
            }
            catch (Exception error) { Console.Error.WriteLine(error); app.Shutdown(1); }
        }));
        try
        {
            Check(app.Run() == 0 && passed, "Feature lifecycle failed.");
            Console.WriteLine("PASS: routed units/intervals, measured traffic persistence, bilingual history/graph, keyboard move/lock/reset/history, shortcut conflicts/save rollback/message/cleanup and accessible names.");
            return 0;
        }
        finally
        {
            if (Path.GetFullPath(root).StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
        }
    }
    private static void Click(MenuItem item)
    {
        typeof(MenuItem).GetMethod("OnClick", Private)!.Invoke(item, null);
        item.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static void Drain(Window window)
    { window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); }
    private static void Render(HistoryWindow history)
    {
        Drain(history);
        var content = (FrameworkElement)history.Content;
        var dpi = VisualTreeHelper.GetDpi(content);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
            drawing.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        bitmap.Render(visual);
        string? path = Environment.GetEnvironmentVariable("TRAFFICSENSE_FEATURE_SCREENSHOT");
        if (string.IsNullOrEmpty(path)) return;
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
