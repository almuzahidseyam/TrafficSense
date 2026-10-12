using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using TrafficSense;
using Forms = System.Windows.Forms;

internal static class ErrorHandlingProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void RunAll()
    {
        string root = Path.Combine(Path.GetTempPath(), "TrafficSense.ErrorProbe");
        string directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (string mode in new[] { "startup", "startup-event", "ui", "close-failure", "blocked-log", "reporting-failure", "recoverable", "background" })
            {
                string caseDirectory = Path.Combine(directory, mode);
                Directory.CreateDirectory(caseDirectory);
                var start = new ProcessStartInfo(Environment.ProcessPath!)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                    start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "TrafficSense.LifecycleTests.dll"));
                foreach (string arg in new[] { "--error-case", mode, caseDirectory }) start.ArgumentList.Add(arg);
                using var child = Process.Start(start)!;
                var output = child.StandardOutput.ReadToEndAsync();
                var errors = child.StandardError.ReadToEndAsync();
                if (!child.WaitForExit(15000))
                {
                    child.Kill();
                    throw new InvalidOperationException($"Error case {mode} did not exit.");
                }
                string stdout = output.GetAwaiter().GetResult();
                string stderr = errors.GetAwaiter().GetResult();
                bool success = mode == "background" ? child.ExitCode != 0 : child.ExitCode == 0;
                Check(success, $"Error case {mode} failed: {stdout}\n{stderr}");
                if (mode == "background")
                {
                    string log = File.ReadAllText(Path.Combine(caseDirectory, "logs", "errors.jsonl"));
                    Check(log.Contains("BackgroundUnhandled") && log.Contains("injected background failure"), "Terminating worker did not persist diagnostics.");
                    Check(!File.Exists(Path.Combine(caseDirectory, "dialog.txt")), "Terminating worker displayed a dialog.");
                }
                Console.WriteLine($"PASS: isolated error handling case {mode}.");
            }
        }
        finally
        {
            Check(Path.GetFullPath(directory).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Unsafe cleanup path.");
            Directory.Delete(directory, recursive: true);
        }
    }

    public static int RunCase(string mode, string directory)
    {
        if (mode == "background") SetErrorMode(0x0002); // Suppress OS crash UI for this disposable child only.
        string scope = @"Local\TrafficSense.ErrorProbe." + Guid.NewGuid().ToString("N");
        string settingsPath = Path.Combine(directory, "settings.json");
        var store = new SettingsStore(settingsPath);
        Check(store.Save(new AppSettings { Language = "bn" }).Success, "Could not seed settings.");
        byte[] original = File.ReadAllBytes(settingsPath);
        string logDirectory = Path.Combine(directory, "logs");
        if (mode == "blocked-log") File.WriteAllText(logDirectory, "blocks directory creation");
        var diagnostics = new DiagnosticLog(logDirectory);
        MainWindow? window = null;
        DispatcherTimer? timer = null;
        Forms.NotifyIcon? notify = null;
        int saves = 0, dialogs = 0;
        string? shown = null;

        var app = new App(scope, () =>
        {
            if (mode == "startup") throw new InvalidOperationException("injected startup failure");
            return window = new MainWindow(store.Load(), settings =>
            {
                saves++;
                if (mode == "close-failure") throw new InvalidOperationException("injected close failure");
                return mode == "recoverable" ? new SettingsSaveResult(false, "injected save failure") : store.Save(settings);
            }, _ => { }, diagnostics: diagnostics);
        }, diagnostics, (message, title) =>
        {
            dialogs++;
            shown = message;
            File.WriteAllText(Path.Combine(directory, "dialog.txt"), message);
            if (window != null)
                Check(timer is { IsEnabled: false } && notify is { Visible: false } && !window.IsEnabled,
                    "Fatal dialog appeared before monitoring/tray stopped or controls were disabled.");
            if (mode == "reporting-failure") throw new InvalidOperationException("injected reporting failure");
        });
        app.InitializeComponent();
        var style = new Style(typeof(MainWindow));
        style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
        style.Setters.Add(new Setter(Window.ShowActivatedProperty, false));
        app.Resources.Add(typeof(MainWindow), style);

        if (mode == "startup-event") app.Startup += (_, _) => throw new InvalidOperationException("injected startup event failure");
        if (!mode.StartsWith("startup")) app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            timer = (DispatcherTimer)typeof(MainWindow).GetField("_timer", Private)!.GetValue(window)!;
            object tray = typeof(App).GetField("_tray", Private)!.GetValue(app)!;
            notify = (Forms.NotifyIcon)tray.GetType().GetField("_notifyIcon", Private)!.GetValue(tray)!;
            if (mode == "background")
            {
                new Thread(() => throw new InvalidOperationException("injected background failure")) { IsBackground = true }.Start();
                return;
            }
            if (mode == "recoverable")
            {
                typeof(MainWindow).GetMethod("MenuTheme_Click", Private)!.Invoke(window, [window, new RoutedEventArgs()]);
                Check(window!.IsVisible && timer.IsEnabled && dialogs == 0, "Recoverable save failure stopped the widget.");
                Check(File.ReadAllBytes(settingsPath).SequenceEqual(original), "Failed save changed previous preferences.");
                var taskError = new UnobservedTaskExceptionEventArgs(new AggregateException(new IOException("injected unobserved task")));
                typeof(App).GetMethod("TaskScheduler_UnobservedTaskException", Private)!.Invoke(app, [null, taskError]);
                Check(taskError.Observed && timer.IsEnabled && dialogs == 0, "Unobserved-task reporting affected the UI.");
                window.Close();
                return;
            }
            var current = (AppSettings)typeof(MainWindow).GetField("_settings", Private)!.GetValue(window)!;
            current.Theme = "Light"; // Fatal close must not persist potentially invalid in-memory state.
            if (mode == "close-failure") { window!.Close(); return; }
            throw new InvalidOperationException("injected UI failure", new IOException("inner diagnostic detail"));
        }));

        int exit = app.Run();
        Check(exit == (mode == "recoverable" ? 0 : 1), "Unexpected app exit code.");
        Check(timer == null || !timer.IsEnabled, "Exit left monitoring active.");
        Check(File.ReadAllBytes(settingsPath).SequenceEqual(original), "Fatal shutdown overwrote saved preferences.");
        Check(saves == (mode == "recoverable" ? 2 : mode == "close-failure" ? 1 : 0), "Unexpected settings write during error handling.");
        if (mode != "recoverable")
        {
            Check(dialogs == 1 && shown != null, "Fatal reporting did not occur exactly once.");
            Check(!shown!.Contains("injected") && !shown.Contains("StackTrace"), "Raw diagnostics leaked into the user message.");
            Check(shown.Contains(mode.StartsWith("startup") ? "could not start" : "অ্যাপটি বন্ধ হবে"), "Fatal message used the wrong language.");
            Check(shown.Contains(mode == "blocked-log" ? "সংরক্ষণ করা যায়নি" : diagnostics.FilePath), "Log availability message was incorrect.");
        }
        if (mode != "blocked-log")
        {
            string[] events = File.ReadAllLines(diagnostics.FilePath).Select(line =>
            {
                using var record = JsonDocument.Parse(line);
                return record.RootElement.GetProperty("Event").GetString()!;
            }).ToArray();
            Check(events.Contains(mode.StartsWith("startup") ? "Startup" : mode == "recoverable" ? "SettingsSaveFailed" : "DispatcherUnhandled"), "Expected diagnostic record was missing.");
            if (mode == "reporting-failure") Check(events.Contains("FatalReporting"), "Reporting failure was not logged.");
            if (mode == "recoverable") Check(events.Contains("UnobservedTask"), "Unobserved task was not logged.");
        }
        using var restarted = new SingleInstanceGate(scope);
        Check(restarted.IsPrimary, "Error shutdown did not release instance ownership.");
        return 0;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);
}
