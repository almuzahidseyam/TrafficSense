using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TrafficSense;
using Forms = System.Windows.Forms;

internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--soak") return SoakProbe.Run(args[1..]);
        if (args.FirstOrDefault() == "--screenshots" && args.Length == 2) return ScreenshotProbe.Run(args[1]);
        if (args.FirstOrDefault() == "--graph-quality") return GraphQualityProbe.Run();
        if (args.FirstOrDefault() == "--network-recovery") return NetworkRecoveryProbe.Run();
        if (args.FirstOrDefault() == "--system-metrics") return SystemMetricsProbe.Run();
        if (args.FirstOrDefault() == "--taskbar-panel") return TaskbarPanelProbe.Run();
        if (args.FirstOrDefault() == "--customization") return CustomizationProbe.Run();
        if (args.FirstOrDefault() == "--sensor-bridge-probe") return SensorBridgeProbe.Run();
        if (args.FirstOrDefault() == "--sensor-bridge") return SensorBridgeProbe.VerifyEntry();
        if (args.FirstOrDefault() == "--updates") return UpdateProbe.Run();
        if (args.FirstOrDefault() == "--appearance") return AppearanceProbe.Run();
        if (args.FirstOrDefault() == "--appearance-restart") return AppearanceProbe.VerifyRestart(args[1]);
        if (args.FirstOrDefault() == "--position-lock") return PositionLockProbe.Run();
        if (args.FirstOrDefault() == "--features") return FeatureProbe.Run();
        if (args.FirstOrDefault() == "--backup") return BackupProbe.Run();
        if (args.FirstOrDefault() == "--csv") return CsvProbe.Run();
        if (args.FirstOrDefault() == "--position-lock-restart") return PositionLockProbe.VerifyRestart(args[1], bool.Parse(args[2]));
        if (args.FirstOrDefault() == "--update-live") return UpdateProbe.CheckLive();
        if (args.FirstOrDefault() is "--about" or "--about-fatal") return AboutProbe.Run(args[0] == "--about-fatal");
        if (args.FirstOrDefault() == "--error-case") return ErrorHandlingProbe.RunCase(args[1], args[2]);
        if (args.FirstOrDefault() == "--error-handling") { ErrorHandlingProbe.RunAll(); return 0; }
        if (args.Length == 0) VerifyPackagedEntryPoint();
        if (args.Length == 2) return VerifyRestart(args[0], args[1]);
        if (args.Length == 0)
        {
            RunChild("--network-recovery");
            RunChild("--graph-quality");
            RunChild("--system-metrics");
            RunChild("--taskbar-panel");
            RunChild("--customization");
            RunChild("--sensor-bridge-probe");
            RunChild("--sensor-bridge");
            RunChild("--about");
            RunChild("--about-fatal");
            RunChild("--updates");
            RunChild("--appearance");
            RunChild("--position-lock");
            RunChild("--features");
            RunChild("--backup");
            RunChild("--csv");
            ErrorHandlingProbe.RunAll();
        }
        string directory = Path.Combine(Path.GetTempPath(), "TrafficSense.Lifecycle", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settings.json");
        try { return RunLifecycle(args, path); }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

    private static int RunLifecycle(string[] args, string settingsPath)
    {
        string scope = args.Length == 1 ? args[0] : @"Local\TrafficSense.Lifecycle." + Guid.NewGuid().ToString("N");
        var store = new SettingsStore(settingsPath);
        const string missingAdapter = "lifecycle-missing-adapter";
        Check(store.Save(new AppSettings { SelectedAdapterId = missingAdapter }).Success, "Could not prepare isolated settings.");
        string? saveError = null;
        var app = new App(scope, () => new MainWindow(store.Load(), store.Save, message => saveError = message));
        app.InitializeComponent();
        if (args.Length == 1)
        {
            int result = app.Run();
            Check(app.MainWindow == null, "Secondary launch created a window.");
            return result;
        }

        MakeInvisible(app);

        int exitCode = 1;
        DispatcherTimer? timer = null;
        Forms.NotifyIcon? notify = null;
        Forms.ContextMenuStrip? menu = null;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            var window = (MainWindow)app.MainWindow;
            try
            {
                Check(window.IsVisible && app.Windows.Count == 1, "Primary widget did not start.");
                VerifyBundledRuntime();
                timer = (DispatcherTimer)typeof(MainWindow).GetField("_timer", Private)!.GetValue(window)!;
                var tray = typeof(App).GetField("_tray", Private)!.GetValue(app)!;
                notify = (Forms.NotifyIcon)tray.GetType().GetField("_notifyIcon", Private)!.GetValue(tray)!;
                menu = notify.ContextMenuStrip!;
                var show = (Forms.ToolStripMenuItem)menu.Items[0];
                Check(notify.Visible && show.Checked && notify.Icon != null, "Tray did not initialize.");

                var trayLanguages = menu.Items.OfType<Forms.ToolStripMenuItem>().Single(item => item.Tag as string == "language");
                var trayEnglish = (Forms.ToolStripMenuItem)trayLanguages.DropDownItems[0];
                var trayBangla = (Forms.ToolStripMenuItem)trayLanguages.DropDownItems[1];
                var english = (MenuItem)window.FindName("MenuEnglish");
                var bangla = (MenuItem)window.FindName("MenuBangla");
                var adapterMenu = (MenuItem)window.FindName("MenuNetworkAdapter");
                var download = (TextBlock)window.FindName("TxtDownload");
                Check(english.IsChecked && trayEnglish.Checked && !bangla.IsChecked, "Default language checks are incorrect.");

                // A real routed click exercises checkbox correction as well as
                // persistence and the shared widget/tray language change event.
                var calculator = typeof(MainWindow).GetField("_speedCalculator", Private)!.GetValue(window)!;
                var baselineField = calculator.GetType().GetField("_previousTimestamp", Private)!;
                object? baseline = baselineField.GetValue(calculator);
                Click(bangla);
                Check(window.Localization.Code == "bn" && store.Load().Language == "bn", "Bangla was not saved.");
                Check(bangla.IsChecked && !english.IsChecked && trayBangla.Checked && !trayEnglish.Checked, "Language checks did not synchronize.");
                Check(adapterMenu.Header.ToString() == "নেটওয়ার্ক অ্যাডাপ্টার" && show.Text == "উইজেট দেখান", "Menus did not translate immediately.");
                Check(download.ToolTip.ToString() == "ডাউনলোডের গতি" && notify.Text.Contains("ডাবল-ক্লিক"), "Tooltips did not translate.");
                Check(((Border)window.Content).ToolTip.ToString()!.Contains("অনুপলব্ধ"), "Disconnected tooltip did not translate.");
                Check(adapterMenu.Items.OfType<MenuItem>().Single(item => item.IsChecked).Header.ToString() == "নির্বাচিত অ্যাডাপ্টার (অনুপলব্ধ)", "Dynamic adapter menu did not translate.");
                Check(Equals(baseline, baselineField.GetValue(calculator)) && store.Load().SelectedAdapterId == missingAdapter,
                    "Language switch changed the sampling baseline or adapter selection.");

                byte[] saved = File.ReadAllBytes(settingsPath);
                using (var locked = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    Click(english);
                Check(saveError?.StartsWith("সেটিং সংরক্ষণ করা যায়নি।") == true, "Save failure did not use the previous UI language.");
                Check(window.Localization.Code == "bn" && bangla.IsChecked && !english.IsChecked && trayBangla.Checked,
                    "Failed save changed the language or menu checks.");
                Check(saved.SequenceEqual(File.ReadAllBytes(settingsPath)), "Failed language save changed the previous file.");

                trayEnglish.PerformClick();
                Check(window.Localization.Code == "en" && english.IsChecked && show.Text == "Show widget" && store.Load().Language == "en",
                    "Tray language selection did not update the widget or persist English.");

                window.Left += 5;
                window.ShowWidget();
                double left = window.Left, top = window.Top;
                show.PerformClick();
                Check(!window.IsVisible && !show.Checked && timer.IsEnabled, "Hide stopped monitoring or left the widget visible.");
                download.Text = "hidden sample sentinel";
                var background = (NetworkSamplingService)typeof(MainWindow).GetField("_sampler", Private)!.GetValue(window)!;
                TimeSpan hiddenBaseline = background.LatestTimestamp;
                // Deliberately keep the dispatcher busy. The production worker
                // must publish a new counter read without dispatcher pumping.
                Check(SpinWait.SpinUntil(() => background.LatestTimestamp > hiddenBaseline, TimeSpan.FromSeconds(4)),
                    "Background sampling stopped while the UI thread was busy.");
                typeof(MainWindow).GetMethod("RefreshNetworkSpeed", Private)!.Invoke(window, null);
                Check(download.Text == "hidden sample sentinel" && background.LatestTimestamp > hiddenBaseline,
                    "Hidden sampling stopped or updated the hidden text/layout.");
                trayBangla.PerformClick();
                Check(!window.IsVisible && timer.IsEnabled && bangla.IsChecked && show.Text == "উইজেট দেখান",
                    "Language change while hidden showed/stopped the widget or failed to translate.");
                typeof(Forms.NotifyIcon).GetMethod("OnDoubleClick", Private)!.Invoke(notify, new object[] { EventArgs.Empty });
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
                Check(window.IsVisible && show.Checked, "Tray double-click did not show widget.");
                var latestSpeed = (NetworkSpeed)typeof(MainWindow).GetField("_latestSpeed", Private)!.GetValue(window)!;
                Check(download.Text == SpeedFormatter.Format(latestSpeed.DownloadBytesPerSecond), "Showing the widget did not render the latest sampled speed.");
                Check(Math.Abs(window.Left - left) < 1 && Math.Abs(window.Top - top) < 1, "Hide/show changed the dragged position.");

                typeof(MainWindow).GetMethod("MenuHide_Click", Private)!.Invoke(window, new object[] { window, new RoutedEventArgs() });
                Check(!window.IsVisible && !show.Checked, "Widget hide did not update tray state.");
                show.PerformClick();
                Check(window.IsVisible && show.Checked, "Tray Show widget command did not restore the widget.");
                show.PerformClick();
                Check(!window.IsVisible, "Tray did not hide the widget before the second launch.");

                // Launch this same host under the same isolated instance name.
                // The real production OnStartup must signal this window and exit.
                RunChild(scope);
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Check(window.IsVisible && show.Checked && app.Windows.Count == 1, "Secondary launch did not restore the one existing widget.");
                exitCode = 0;
                menu.Items.OfType<Forms.ToolStripMenuItem>().Single(item => item.Tag as string == "exit").PerformClick();
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
        using (var restarted = new SingleInstanceGate(scope))
            Check(restarted.IsPrimary, "Exit did not release single-instance ownership.");
        RunChild(scope, settingsPath);
        Console.WriteLine("PASS: live English/Bangla menus and tooltips, isolated persistence and restart, locked-file language rollback, hidden language changes, unchanged sampling/adapter, tray controls, second-process activation, Exit cleanup.");
        return 0;
    }

    private static void Click(MenuItem item)
    {
        typeof(MenuItem).GetMethod("OnClick", Private)!.Invoke(item, null);
        // WPF queues the click event so its popup can close before running it.
        item.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
    }

    private static void MakeInvisible(App app)
    {
        var style = new Style(typeof(MainWindow));
        style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
        style.Setters.Add(new Setter(Window.ShowActivatedProperty, false));
        app.Resources.Add(typeof(MainWindow), style);
    }

    internal static void RunChild(params string[] args)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "TrafficSense.LifecycleTests.dll"));
        foreach (string arg in args) start.ArgumentList.Add(arg);
        using var child = Process.Start(start)!;
        var output = child.StandardOutput.ReadToEndAsync();
        var errors = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit(10000))
        {
            child.Kill();
            throw new InvalidOperationException("Child process did not exit.");
        }
        Console.Write(output.GetAwaiter().GetResult());
        Console.Error.Write(errors.GetAwaiter().GetResult());
        Check(child.ExitCode == 0, "Child process failed.");
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("SingleFile", "IL3000",
        Justification = "This probe deliberately verifies that CoreLib has no external assembly file in the single-file bundle.")]
    internal static void VerifyBundledRuntime()
    {
        if (Environment.GetEnvironmentVariable("TRAFFICSENSE_VERIFY_BUNDLE") != "1") return;
        string expectedVersion = Environment.GetEnvironmentVariable("TRAFFICSENSE_EXPECTED_VERSION")!;
        var appAssembly = typeof(MainWindow).Assembly;
        string? informationalVersion = appAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Check(!string.IsNullOrEmpty(expectedVersion) && informationalVersion?.Split('+')[0] == expectedVersion &&
            appAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version == expectedVersion + ".0",
            "Bundled app version does not match the release version.");
        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR")!) + Path.DirectorySeparatorChar;
        using var process = Process.GetCurrentProcess();
        Check(typeof(object).Assembly.Location.Length == 0, "CoreLib was loaded from an external runtime assembly instead of the bundle.");
        // The Windows .NET single-file host can statically link CoreCLR, so no
        // coreclr.dll module exists in that mode. Reject an external one if present.
        foreach (var module in process.Modules.Cast<ProcessModule>().Where(item => item.ModuleName.Equals("coreclr.dll", StringComparison.OrdinalIgnoreCase)))
            Check(module.FileName.StartsWith(root, StringComparison.OrdinalIgnoreCase), "CoreCLR was loaded outside the bundled runtime.");
        foreach (string name in new[] { "PresentationNative_cor3.dll", "wpfgfx_cor3.dll" })
        {
            var module = process.Modules.Cast<ProcessModule>().SingleOrDefault(item => item.ModuleName.Equals(name, StringComparison.OrdinalIgnoreCase));
            Check(module != null, $"{name} was not loaded. Modules: {string.Join(", ", process.Modules.Cast<ProcessModule>().Select(item => item.ModuleName))}");
            Check(module!.FileName.StartsWith(root, StringComparison.OrdinalIgnoreCase), $"{name} was not loaded from the bundled runtime: {module.FileName}");
        }
        Console.WriteLine($"PASS: app version {expectedVersion}, embedded CoreLib and bundled WPF native libraries, with installed-runtime lookup disabled.");
    }

    private static void VerifyPackagedEntryPoint()
    {
        string? executable = Environment.GetEnvironmentVariable("TRAFFICSENSE_PACKAGE_EXE");
        if (string.IsNullOrEmpty(executable)) return;
        using var identity = WindowsIdentity.GetCurrent();
        using var gate = new SingleInstanceGate(@"Local\TrafficSense." + identity.User!.Value);
        if (!gate.IsPrimary)
        {
            Console.WriteLine("SKIP: packaged entry-point activation check; the user's existing widget is running. Isolated portable UI checks still run.");
            return;
        }
        // Hold the real instance gate so the distributed EXE exercises its true
        // entry point and runtime without loading/saving the user's settings.
        using var activated = new ManualResetEventSlim();
        gate.Listen(activated.Set);
        string tracePath = Path.Combine(Path.GetDirectoryName(executable)!, "host-trace.log");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        start.Environment["COREHOST_TRACE"] = "1";
        start.Environment["COREHOST_TRACEFILE"] = tracePath;
        using var child = Process.Start(start)!;
        if (!child.WaitForExit(15000))
        {
            child.Kill();
            throw new InvalidOperationException("Packaged entry point did not exit as a secondary instance.");
        }
        Check(child.ExitCode == 0 && activated.Wait(5000), "Packaged entry point failed to activate the primary instance.");
        string root = Path.GetFullPath(Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR")!);
        string[] trace = File.ReadAllLines(tracePath);
        Check(trace.Any(line => line.Contains("CoreCLR path = '', CoreCLR dir = ''", StringComparison.Ordinal) ||
            (line.Contains("CoreCLR path", StringComparison.OrdinalIgnoreCase) && line.Contains(root, StringComparison.OrdinalIgnoreCase) &&
             line.Contains("coreclr.dll", StringComparison.OrdinalIgnoreCase))),
            "Packaged entry point did not load its own bundled CoreCLR.");
        Console.WriteLine("PASS: distributed EXE started from the extracted ZIP, used bundled CoreCLR and signaled the primary instance without touching user settings.");
    }

    private static int VerifyRestart(string scope, string settingsPath)
    {
        var store = new SettingsStore(settingsPath);
        var app = new App(scope, () => new MainWindow(store.Load(), store.Save));
        app.InitializeComponent();
        MakeInvisible(app);
        bool passed = false;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var window = (MainWindow)app.MainWindow;
                var tray = typeof(App).GetField("_tray", Private)!.GetValue(app)!;
                var notify = (Forms.NotifyIcon)tray.GetType().GetField("_notifyIcon", Private)!.GetValue(tray)!;
                Check(window.Localization.Code == "bn" && ((MenuItem)window.FindName("MenuBangla")).IsChecked &&
                    ((MenuItem)window.FindName("MenuHide")).Header.ToString() == "উইজেট লুকান" &&
                    notify.ContextMenuStrip!.Items[0].Text == "উইজেট দেখান", "Saved language was not applied at restart.");
                passed = true;
                window.Close();
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                app.Shutdown(1);
            }
        }));
        int exit = app.Run();
        Check(passed && exit == 0, "Restart language check failed.");
        return 0;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
