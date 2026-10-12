using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using TrafficSense;
using Forms = System.Windows.Forms;

internal static class PositionLockProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static int Run()
    {
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TrafficSense.PositionLockTests"));
        string root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "settings.json");
        var store = new SettingsStore(path);
        Check(store.Save(new AppSettings { SelectedAdapterId = "lock-manual", WidgetFontSize = 18,
            BackgroundOpacity = 64, Theme = "Light" }).Success, "Could not create isolated settings.");
        var errors = new List<string>();
        int saves = 0, drags = 0, changes = 0;
        MainWindow? window = null;
        var app = CreateApp(() => window = new MainWindow(store.Load(), settings => { saves++; return store.Save(settings); },
            errors.Add, readAdapters: () => [], sampleTimestamp: () => TimeSpan.FromSeconds(1),
            dragMove: () => { drags++; window!.Left += 5; }));
        bool passed = false;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var widget = window!;
                Drain(widget);
                var item = (MenuItem)widget.FindName("MenuPositionLock");
                var tray = (TrayController)typeof(App).GetField("_tray", Private)!.GetValue(app)!;
                var menu = (Forms.ContextMenuStrip)typeof(TrayController).GetField("_menu", Private)!.GetValue(tray)!;
                var trayLock = menu.Items.OfType<Forms.ToolStripMenuItem>().Single(i => i.Tag as string == "position-lock");
                widget.PositionLockChanged += (_, _) => changes++;
                Check(!widget.PositionLocked && !item.IsChecked && !trayLock.Checked, "Legacy/default settings locked dragging.");
                Check(!widget.TryMoveByDrag(MouseButtonState.Released) && drags == 0, "Released button initiated a drag.");
                double left = widget.Left;
                Check(widget.TryMoveByDrag(MouseButtonState.Pressed) && drags == 1 && widget.Left != left,
                    "Unlocked drag did not reach the movement operation.");
                var calculator = typeof(MainWindow).GetField("_speedCalculator", Private)!.GetValue(widget)!;
                var baselineField = calculator.GetType().GetField("_previousTimestamp", Private)!;
                object? baseline = baselineField.GetValue(calculator);
                left = widget.Left;
                Click(item);
                Check(widget.PositionLocked && item.IsChecked && trayLock.Checked && changes == 1, "Widget lock did not synchronize.");
                var lockedSettings = store.Load();
                var bounds = Bounds(widget);
                Check(lockedSettings.PositionLocked && lockedSettings.ScreenLeft == bounds.Left && lockedSettings.ScreenTop == bounds.Top,
                    "Lock did not immediately save the current position.");
                Check(!widget.TryMoveByDrag(MouseButtonState.Pressed) && drags == 1 && widget.Left == left,
                    "Locked widget could still be dragged.");
                int beforeSaves = saves;
                widget.ChangePositionLock(true);
                Check(saves == beforeSaves && changes == 1 && item.IsChecked, "Unchanged lock wrote settings or published a change.");
                widget.ChangeLanguage("bn");
                Check(item.Header.ToString() == "অবস্থান লক করুন" && trayLock.Text == "অবস্থান লক করুন" && trayLock.Checked,
                    "Locked menus did not translate.");
                byte[] saved = File.ReadAllBytes(path);
                using (var blocked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) Click(item);
                Check(widget.PositionLocked && item.IsChecked && trayLock.Checked && changes == 1,
                    "Failed widget unlock changed the saved state or menu checks.");
                Check(saved.SequenceEqual(File.ReadAllBytes(path)), "Failed unlock changed the settings file.");
                trayLock.PerformClick();
                Check(!widget.PositionLocked && !item.IsChecked && !trayLock.Checked && changes == 2,
                    "Tray unlock did not synchronize.");
                Check(widget.TryMoveByDrag(MouseButtonState.Pressed) && drags == 2, "Unlock did not re-enable dragging.");
                var settings = (AppSettings)typeof(MainWindow).GetField("_settings", Private)!.GetValue(widget)!;
                var previousPosition = (settings.WindowLeft, settings.WindowTop, settings.ScreenLeft, settings.ScreenTop);
                saved = File.ReadAllBytes(path);
                using (var blocked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) trayLock.PerformClick();
                Check(!widget.PositionLocked && !item.IsChecked && !trayLock.Checked && changes == 2 &&
                    previousPosition == (settings.WindowLeft, settings.WindowTop, settings.ScreenLeft, settings.ScreenTop),
                    "Failed tray lock did not roll back state/position/checks.");
                Check(saved.SequenceEqual(File.ReadAllBytes(path)) && errors.Count == 2 &&
                    errors.All(message => message.StartsWith("সেটিং সংরক্ষণ করা যায়নি।")), "Save failure damaged settings or used the wrong language.");
                widget.Hide();
                trayLock.PerformClick();
                Check(widget.PositionLocked && trayLock.Checked && item.IsChecked && !widget.IsVisible &&
                    ((DispatcherTimer)typeof(MainWindow).GetField("_timer", Private)!.GetValue(widget)!).IsEnabled,
                    "Locking from the tray changed visibility or stopped monitoring.");
                Check(Equals(baseline, baselineField.GetValue(calculator)), "Position locking reset the sampling baseline.");
                widget.Show();
                Drain(widget);
                var handle = new WindowInteropHelper(widget).Handle;
                foreach (int message in new[] { 0x007E, 0x001A }) // display/work-area notifications
                {
                    Check(SetWindowPos(handle, IntPtr.Zero, -30000, -30000, 0, 0, 0x0001 | 0x0004 | 0x0010), "Could not move isolated test window.");
                    SendMessage(handle, message, IntPtr.Zero, IntPtr.Zero);
                    Drain(widget);
                    CheckInsideWorkArea(widget);
                    Check(widget.PositionLocked && !widget.TryMoveByDrag(MouseButtonState.Pressed), "Monitor recovery unlocked dragging.");
                }
                // Font-size growth must still clamp a locked widget at the edge.
                var work = Forms.Screen.FromHandle(handle).WorkingArea;
                bounds = Bounds(widget);
                Check(SetWindowPos(handle, IntPtr.Zero, work.Right - (bounds.Right - bounds.Left),
                    work.Bottom - (bounds.Bottom - bounds.Top), 0, 0, 0x0001 | 0x0004 | 0x0010), "Could not position test window at an edge.");
                Click(((MenuItem)widget.FindName("MenuFontSize")).Items.OfType<MenuItem>().Single(i => (int)i.Tag == 24));
                Drain(widget);
                CheckInsideWorkArea(widget);
                Check(widget.PositionLocked && store.Load().SelectedAdapterId == "lock-manual" && store.Load().Theme == "Light",
                    "Lock or recovery changed other preferences.");
                widget.Hide();
                menu.Items.OfType<Forms.ToolStripMenuItem>().Single(i => i.Tag as string == "reset-position").PerformClick();
                Check(!widget.IsVisible && widget.PositionLocked, "Reset changed visibility or lock.");
                widget.Show();
                Drain(widget);
                CheckInsideWorkArea(widget);
                var resetBounds = Bounds(widget);
                saved = File.ReadAllBytes(path);
                using (var blocked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    Check(!widget.ResetPosition(), "Blocked reset reported success.");
                Check(Bounds(widget).Left == resetBounds.Left && Bounds(widget).Top == resetBounds.Top &&
                    saved.SequenceEqual(File.ReadAllBytes(path)), "Failed reset changed position or settings.");
                // Verify exact restart coordinates away from an edge: startup's
                // placeholder speed can be wider and legitimately require clamping.
                Check(SetWindowPos(handle, IntPtr.Zero, work.Left + 50, work.Top + 50, 0, 0,
                    0x0001 | 0x0004 | 0x0010), "Could not position test window for restart.");
                Drain(widget);
                var timer = (DispatcherTimer)typeof(MainWindow).GetField("_timer", Private)!.GetValue(widget)!;
                widget.Hide();
                Check(widget.ChangeMonitorPreferences(true, 500) && timer.Interval == TimeSpan.FromMilliseconds(500) && !widget.IsVisible,
                    "Monitor preferences did not apply while hidden.");
                saved = File.ReadAllBytes(path);
                using (var blocked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    Check(!widget.ChangeMonitorPreferences(false, 2000), "Blocked monitor preference change succeeded.");
                Check(timer.Interval == TimeSpan.FromMilliseconds(500) && store.Load().SpeedInBits && saved.SequenceEqual(File.ReadAllBytes(path)),
                    "Failed preference save changed interval/units.");
                Check(widget.ChangeMonitorPreferences(false, 1000), "Could not restore monitor preferences.");
                widget.Show();
                Drain(widget);
                // Disposed tray controllers must no longer receive lock events.
                using (var temporary = new TrayController(widget)) { }
                widget.ChangePositionLock(false);
                widget.ChangePositionLock(true);
                passed = true;
                widget.Close();
            }
            catch (Exception error) { Console.Error.WriteLine(error); app.Shutdown(1); }
        }));
        try
        {
            Check(app.Run() == 0 && passed, "Position lock lifecycle failed.");
            Program.RunChild("--position-lock-restart", path, "true");
            // The restart child unlocks and saves; the following launch must stay unlocked.
            Program.RunChild("--position-lock-restart", path, "false");
            Console.WriteLine("PASS: position lock drag guard, bilingual widget/tray synchronization, hidden monitoring, save/coordinate rollback, automatic recovery, sizing and locked/unlocked restart.");
            return 0;
        }
        finally
        {
            if (Path.GetFullPath(root).StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(root, recursive: true);
        }
    }

    internal static int VerifyRestart(string path, bool expectedLocked)
    {
        var store = new SettingsStore(path);
        int drags = 0;
        var app = CreateApp(() => new MainWindow(store.Load(), store.Save, message => throw new InvalidOperationException(message),
            readAdapters: () => [], dragMove: () => drags++));
        bool passed = false;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var widget = (MainWindow)app.MainWindow;
                Drain(widget);
                var tray = (TrayController)typeof(App).GetField("_tray", Private)!.GetValue(app)!;
                var menu = (Forms.ContextMenuStrip)typeof(TrayController).GetField("_menu", Private)!.GetValue(tray)!;
                Check(widget.PositionLocked == expectedLocked && ((MenuItem)widget.FindName("MenuPositionLock")).IsChecked == expectedLocked &&
                    menu.Items.OfType<Forms.ToolStripMenuItem>().Single(i => i.Tag as string == "position-lock").Checked == expectedLocked,
                    "Restart lost the lock state/checkmarks.");
                var saved = store.Load();
                var bounds = Bounds(widget);
                Check(saved.ScreenLeft == bounds.Left && saved.ScreenTop == bounds.Top && widget.Localization.Code == "bn" &&
                    saved.WidgetFontSize == 24 && saved.BackgroundOpacity == 64 && saved.Theme == "Light" && saved.SelectedAdapterId == "lock-manual",
                    $"Restart lost the saved position or other preferences: saved=({saved.ScreenLeft},{saved.ScreenTop}), actual=({bounds.Left},{bounds.Top}), " +
                    $"language={widget.Localization.Code}, font={saved.WidgetFontSize}, alpha={saved.BackgroundOpacity}, theme={saved.Theme}, adapter={saved.SelectedAdapterId}.");
                Check(widget.TryMoveByDrag(MouseButtonState.Pressed) == !expectedLocked && drags == (expectedLocked ? 0 : 1),
                    "Restart did not apply the saved drag permission.");
                CheckInsideWorkArea(widget);
                widget.ChangePositionLock(false);
                passed = true;
                widget.Close();
            }
            catch (Exception error) { Console.Error.WriteLine(error); app.Shutdown(1); }
        }));
        Check(app.Run() == 0 && passed, "Position lock restart failed.");
        return 0;
    }

    private static App CreateApp(Func<MainWindow> createWindow)
    {
        var app = new App(@"Local\TrafficSense.PositionLock." + Guid.NewGuid().ToString("N"), createWindow);
        app.InitializeComponent();
        var style = new Style(typeof(MainWindow));
        style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
        style.Setters.Add(new Setter(Window.ShowActivatedProperty, false));
        app.Resources.Add(typeof(MainWindow), style);
        return app;
    }

    private static void Click(MenuItem item)
    {
        typeof(MenuItem).GetMethod("OnClick", Private)!.Invoke(item, null);
        item.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
    }
    private static void Drain(MainWindow window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    private static NativeRect Bounds(MainWindow window)
    {
        Check(GetWindowRect(new WindowInteropHelper(window).Handle, out var bounds), "Could not read test window bounds.");
        return bounds;
    }
    private static void CheckInsideWorkArea(MainWindow window)
    {
        var work = Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle).WorkingArea;
        var bounds = Bounds(window);
        Check(bounds.Left >= work.Left && bounds.Top >= work.Top && bounds.Right <= work.Right && bounds.Bottom <= work.Bottom,
            "Locked widget was left outside the monitor work area.");
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
