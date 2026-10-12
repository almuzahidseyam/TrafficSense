using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Threading;
using TrafficSense;

internal static class TaskbarPanelProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static int Run()
    {
        var shell = new FakeShell(); var settings = new AppSettings(); bool fail = false;
        var errors = new List<string>();
        var app = new App(@"Local\TrafficSense.Panel." + Guid.NewGuid().ToString("N"), () =>
            new MainWindow(settings, _ => new(!fail, fail ? "blocked" : null), errors.Add, readAdapters: () => [], appBarShell: shell));
        app.InitializeComponent();
        foreach (var type in new[] { typeof(MainWindow), typeof(TaskbarPanelWindow) })
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
                Check(window.ChangeTaskbarPanel(true), "Panel enable failed."); Drain();
                var panel = Panel(window)!; Check(panel.Registered && shell.Registrations == 1 && shell.Bounds.Top < shell.Bounds.Bottom, "Panel registration/position missing.");
                var speed = (TextBlock)typeof(TaskbarPanelWindow).GetField("_speed",Private)!.GetValue(panel)!;
                Check(new TextRange(speed.ContentStart,speed.ContentEnd).Text.Contains(window.Localization[TextKey.NotAvailable]),"Unknown panel sample looked like measured zero.");
                window.Hide(); window.ChangeLanguage("bn"); Drain();
                Check(panel.Title == "টাস্কবারের পাশে প্যানেল" && !window.IsVisible && panel.IsVisible, "Panel and floating visibility/language are coupled.");
                Check(new TextRange(speed.ContentStart,speed.ContentEnd).Text.Contains(window.Localization[TextKey.NotAvailable]),"Hidden widget left panel quality text untranslated.");
                var original = shell.Bounds; Check(window.ChangeTaskbarPanel(true, top: true), "Top-edge save failed."); Drain();
                Check(shell.Bounds.Top < original.Top && shell.LastTop, "Top edge did not reposition.");
                var handle = new WindowInteropHelper(panel).Handle;
                SendMessage(handle, (int)TaskbarPanelWindow.ExplorerCreatedMessage, IntPtr.Zero, IntPtr.Zero); Drain();
                Check(shell.Registrations == 2 && panel.Registered, "Explorer notification failed to register again.");
                SendMessage(handle, (int)TaskbarPanelWindow.CallbackMessage, new(2), new(1)); Check(shell.Fullscreen, "Full-screen app did not lower panel.");
                SendMessage(handle, (int)TaskbarPanelWindow.CallbackMessage, new(2), IntPtr.Zero); Check(!shell.Fullscreen, "Full-screen close did not restore panel.");
                // Exercise our DPI callback without feeding WPF a fabricated
                // WM_DPICHANGED payload (WPF expects a real suggested RECT).
                typeof(TaskbarPanelWindow).GetMethod("Message", Private)!.Invoke(panel,
                    [handle, 0x02E0, IntPtr.Zero, IntPtr.Zero, false]); Drain();
                fail = true; Check(!window.ChangeTaskbarPanel(false) && settings.TaskbarPanel && panel.Registered, "Panel save failure did not preserve active panel.");
                fail = false; Check(window.ChangeTaskbarPanel(false), "Disable failed."); Drain();
                Check(Panel(window) == null && !panel.Registered && shell.Removals == 1, "Disabling did not release reservation/hook.");
                shell.AllowRegister = false; Check(!window.ChangeTaskbarPanel(true) && !settings.TaskbarPanel && Panel(window) == null && errors.Count == 2,
                    "Registration failure retained saved enabled state or failed to report fallback.");
                shell.AllowRegister = true;
                for (int i = 0; i < 12; i++) { Check(window.ChangeTaskbarPanel(true), "Repeated enable failed."); Drain(); Check(window.ChangeTaskbarPanel(false), "Repeated disable failed."); }
                window.ChangeTaskbarPanel(true); var last = Panel(window)!; window.Close();
                Check(!last.Registered && Panel(window) == null, "Exit retained appbar."); result = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); app.Shutdown(1); }
        }));
        int exit = app.Run(); Check(exit == 0 && result == 0, "Panel probe failed.");
        Console.WriteLine("PASS: isolated appbar boundary, bilingual independent panel, top/bottom placement, Explorer/DPI/fullscreen hooks, save/registration fallback, repeated attach/detach and Exit. Desktop reservations were simulated to preserve the user's workspace.");
        return 0;
    }
    private static TaskbarPanelWindow? Panel(MainWindow window) => (TaskbarPanelWindow?)typeof(MainWindow).GetField("_taskbarPanel", Private)!.GetValue(window);
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class FakeShell : IAppBarShell
    {
        public bool AllowRegister = true, Fullscreen, LastTop;
        public int Registrations, Removals; public DockBounds Bounds;
        public bool Register(IntPtr window, uint callback) { Registrations++; return AllowRegister; }
        public DockBounds Query(IntPtr window, bool top, DockBounds bounds) => top ? bounds : bounds with { Bottom = bounds.Bottom - 40 };
        public DockBounds Set(IntPtr window, bool top, DockBounds bounds) { LastTop = top; return Bounds = bounds; }
        public void Move(IntPtr window, DockBounds bounds) { Bounds = bounds; }
        public void Notify(IntPtr window, uint message) { }
        public void Unregister(IntPtr window) { Removals++; }
        public void FullScreen(IntPtr window, bool active) { Fullscreen = active; }
        public void Arrange(IntPtr window, bool hiding) { }
    }
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}
