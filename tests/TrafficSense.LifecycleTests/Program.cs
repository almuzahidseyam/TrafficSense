using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using TrafficSense;
using Forms = System.Windows.Forms;

internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static int Main(string[] args)
    {
        string scope = args.Length == 1 ? args[0] : @"Local\TrafficSense.Lifecycle." + Guid.NewGuid().ToString("N");
        var app = new App(scope);
        app.InitializeComponent();
        if (args.Length == 1)
        {
            int result = app.Run();
            Check(app.MainWindow == null, "Secondary launch created a window.");
            return result;
        }

        var style = new Style(typeof(MainWindow));
        style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
        style.Setters.Add(new Setter(Window.ShowActivatedProperty, false));
        app.Resources.Add(typeof(MainWindow), style);

        int exitCode = 1;
        DispatcherTimer? timer = null;
        Forms.NotifyIcon? notify = null;
        Forms.ContextMenuStrip? menu = null;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            var window = (MainWindow)app.MainWindow;
            // Never save into the real user's settings file from this probe.
            var closing = typeof(MainWindow).GetMethod("Window_Closing", Private)!;
            window.Closing -= (CancelEventHandler)Delegate.CreateDelegate(typeof(CancelEventHandler), window, closing);
            try
            {
                Check(window.IsVisible && app.Windows.Count == 1, "Primary widget did not start.");
                timer = (DispatcherTimer)typeof(MainWindow).GetField("_timer", Private)!.GetValue(window)!;
                var tray = typeof(App).GetField("_tray", Private)!.GetValue(app)!;
                notify = (Forms.NotifyIcon)tray.GetType().GetField("_notifyIcon", Private)!.GetValue(tray)!;
                menu = notify.ContextMenuStrip!;
                var show = (Forms.ToolStripMenuItem)menu.Items[0];
                Check(notify.Visible && show.Checked && notify.Icon != null, "Tray did not initialize.");

                window.Left += 5;
                window.ShowWidget();
                double left = window.Left, top = window.Top;
                show.PerformClick();
                Check(!window.IsVisible && !show.Checked && timer.IsEnabled, "Hide stopped monitoring or left the widget visible.");
                typeof(Forms.NotifyIcon).GetMethod("OnDoubleClick", Private)!.Invoke(notify, new object[] { EventArgs.Empty });
                Check(window.IsVisible && show.Checked, "Tray double-click did not show widget.");
                Check(Math.Abs(window.Left - left) < 1 && Math.Abs(window.Top - top) < 1, "Hide/show changed the dragged position.");

                typeof(MainWindow).GetMethod("MenuHide_Click", Private)!.Invoke(window, new object[] { window, new RoutedEventArgs() });
                Check(!window.IsVisible && !show.Checked, "Widget hide did not update tray state.");
                show.PerformClick();
                Check(window.IsVisible && show.Checked, "Tray Show widget command did not restore the widget.");
                show.PerformClick();
                Check(!window.IsVisible, "Tray did not hide the widget before the second launch.");

                // Launch this same host under the same isolated instance name.
                // The real production OnStartup must signal this window and exit.
                var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
                    start.ArgumentList.Add(typeof(Program).Assembly.Location);
                start.ArgumentList.Add(scope);
                using var child = Process.Start(start)!;
                if (!child.WaitForExit(10000))
                {
                    child.Kill();
                    throw new InvalidOperationException("Secondary process did not exit.");
                }
                Check(child.ExitCode == 0, "Secondary process failed.");
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Check(window.IsVisible && show.Checked && app.Windows.Count == 1, "Secondary launch did not restore the one existing widget.");
                exitCode = 0;
                ((Forms.ToolStripMenuItem)menu.Items[2]).PerformClick();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                app.Shutdown(1);
            }
        }));
        int appExit = app.Run();
        Check(exitCode == 0 && appExit == 0, "Lifecycle probe failed.");
        Check(timer is { IsEnabled: false }, "Exit left the sampling timer running.");
        Check(notify is { Visible: false } && menu is { IsDisposed: true }, "Exit left tray resources alive.");
        using var restarted = new SingleInstanceGate(scope);
        Check(restarted.IsPrimary, "Exit did not release single-instance ownership.");
        Console.WriteLine("PASS: production App startup, tray hide/show/double-click, dragged-position retention, live second-process activation without duplicate, Exit cleanup, mutex release.");
        return 0;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
