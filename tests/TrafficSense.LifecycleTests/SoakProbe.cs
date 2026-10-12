using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TrafficSense;

// Exercises the real application in a separate process, with temporary settings
// and an isolated instance gate. Durations below are wall-clock time, not ticks.
internal static class SoakProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const long MiB = 1024 * 1024;

    public static int Run(string[] args)
    {
        int seconds = 600, iterations = 3000;
        string reportPath = Path.GetFullPath("stability.json");
        for (int i = 0; i < args.Length; i += 2)
        {
            switch (args[i])
            {
                case "--seconds": seconds = int.Parse(args[i + 1]); break;
                case "--iterations": iterations = int.Parse(args[i + 1]); break;
                case "--report": reportPath = Path.GetFullPath(args[i + 1]); break;
                default: throw new ArgumentException($"Unknown soak option: {args[i]}");
            }
        }
        if (seconds < 30 || iterations < 0) throw new ArgumentException("Use at least 30 seconds and nonnegative stress iterations.");

        string directory = Path.Combine(Path.GetTempPath(), "TrafficSense.Soak", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        // Exercise the largest retained daily list without reading or writing user usage.
        var usageStore = new UsageStore(Path.Combine(directory, "usage.json"));
        var today = DateOnly.FromDateTime(DateTimeOffset.Now.Date);
        if (!usageStore.Save(new UsageLedger(Enumerable.Range(0, UsageLedger.MaximumDays)
                .Select(i => new UsageDay(today.AddDays(-i), 1024 * 1024, 512 * 1024)))))
            throw new IOException("Could not seed isolated usage for the stability probe.");
        var errors = new List<string>();
        var samples = new List<Sample>();
        var watch = Stopwatch.StartNew();
        var app = new App(@"Local\TrafficSense.Soak." + Guid.NewGuid().ToString("N"),
            () => new MainWindow(new AppSettings { WindowLeft = 30, WindowTop = 150 }, store.Save, errors.Add, usageStore: usageStore)
                { ShowActivated = false, Topmost = false });
        app.InitializeComponent();
        var historyStyle = new Style(typeof(HistoryWindow));
        historyStyle.Setters.Add(new Setter(Window.ShowActivatedProperty, false));
        historyStyle.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
        app.Resources.Add(typeof(HistoryWindow), historyStyle);
        using var process = Process.GetCurrentProcess();
        MainWindow? window = null;
        DispatcherTimer? monitor = null, sampler = null;
        int ticks = 0, visibleTicks = 0, hiddenTicks = 0;
        bool hidden = false, stressStarted = false;
        double lastTick = 0, maxDelay = 0;
        Sample? retainedBefore = null, retainedAfter = null;
        double warmupEnd = 0, measurementEnd = 0;
        TimeSpan stressDuration = default;
        int completedIterations = 0;
        MethodInfo refresh = typeof(MainWindow).GetMethod("RefreshNetworkSpeed", Private)!;
        MethodInfo adapterMenu = typeof(MainWindow).GetMethod("BuildAdapterMenu", Private)!;
        MethodInfo theme = typeof(MainWindow).GetMethod("MenuTheme_Click", Private)!;
        MethodInfo appearance = typeof(MainWindow).GetMethod("ChangeAppearance", Private)!;

        void Finish()
        {
            foreach (var history in app.Windows.OfType<HistoryWindow>().ToArray()) history.Close();
            // Measure after the final history window and its deferred WPF work close,
            // matching the warmup snapshot where history is also closed.
            app.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                Collect();
                retainedAfter = Capture(process, watch.Elapsed.TotalSeconds, "retained-after", ticks);
                sampler!.Stop();
                window!.Close();
            }));
        }

        void StressBatch()
        {
            try
            {
                // The previous batch allowed a visible frame with its new size.
                if (window!.IsVisible) window.Hide();
                foreach (var history in app.Windows.OfType<HistoryWindow>().ToArray()) history.Close();
                // Yield to rendering/input/timer work between small batches.
                int end = Math.Min(completedIterations + 10, iterations);
                while (completedIterations < end)
                {
                    refresh.Invoke(window, null);
                    if (completedIterations % 25 == 0) adapterMenu.Invoke(window, null);
                    if (completedIterations % 50 == 0)
                    {
                        window!.ChangeLanguage(window.Localization.Code == "en" ? "bn" : "en");
                        theme.Invoke(window, new object[] { window, new RoutedEventArgs() });
                        int appearanceIndex = completedIterations / 50;
                        appearance.Invoke(window, new object[]
                        {
                            WidgetAppearance.FontSizes[appearanceIndex % WidgetAppearance.FontSizes.Count],
                            WidgetAppearance.BackgroundOpacities[appearanceIndex % WidgetAppearance.BackgroundOpacities.Count]
                        });
                        // ShowActivated=false keeps stress cycles from taking
                        // focus away from the user. Activation is covered by
                        // the separate lifecycle probe.
                        window.Show();
                        window.Hide();
                        // Exercise both rapid unrendered toggles and a rendered
                        // size change before the next background batch hides it.
                        window.Show();
                        // Test native icon/menu ownership repeatedly as well as
                        // the one long-lived production tray controller.
                        using var temporaryTray = new TrayController(window);
                        window.ChangePositionLock(!window.PositionLocked);
                        window.ChangeMonitorPreferences(appearanceIndex % 2 == 0,
                            MonitorPreferences.Intervals[appearanceIndex % MonitorPreferences.Intervals.Count]);
                        window.ShowHistory();
                        var history = app.Windows.OfType<HistoryWindow>().Single();
                        ExerciseChartDetails(history);
                        var period = (ComboBox)typeof(HistoryWindow).GetField("_period", Private)!.GetValue(history)!;
                        period.SelectedIndex = appearanceIndex % 3;
                        if (period.SelectedIndex == 2)
                            ((TextBox)typeof(HistoryWindow).GetField("_fromDate", Private)!.GetValue(history)!).Text =
                                appearanceIndex % 2 == 0 ? "2026-01-01" : "invalid";
                    }
                    completedIterations++;
                }
                if (completedIterations < iterations)
                    app.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(StressBatch));
                else
                {
                    stressDuration = watch.Elapsed - TimeSpan.FromSeconds(measurementEnd);
                    // Let deferred WPF/WinForms cleanup drain before retention checks.
                    app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(Finish));
                }
            }
            catch (Exception error)
            {
                errors.Add(error.ToString());
                Finish();
            }
        }

        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            window = (MainWindow)app.MainWindow;
            window.ShowHistory();
            ExerciseChartDetails(app.Windows.OfType<HistoryWindow>().Single());
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            { foreach (var history in app.Windows.OfType<HistoryWindow>().ToArray()) history.Close(); }));
            Program.VerifyBundledRuntime();
            monitor = (DispatcherTimer)typeof(MainWindow).GetField("_timer", Private)!.GetValue(window)!;
            monitor.Tick += (_, _) =>
            {
                ticks++;
                if (stressStarted) return;
                if (hidden) hiddenTicks++; else visibleTicks++;
            };
            warmupEnd = watch.Elapsed.TotalSeconds + 20;
            measurementEnd = warmupEnd + seconds;
            lastTick = watch.Elapsed.TotalSeconds;
            Console.WriteLine($"SOAK: warming up for 20 s, then {seconds} s visible/hidden and {iterations} refresh stress iterations.");
            sampler = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
            sampler.Tick += (_, _) =>
            {
                double now = watch.Elapsed.TotalSeconds;
                maxDelay = Math.Max(maxDelay, Math.Max(0, now - lastTick - 1));
                lastTick = now;
                if (now < warmupEnd) return;
                if (retainedBefore == null)
                {
                    Collect();
                    retainedBefore = Capture(process, now, "retained-before", ticks);
                    samples.Add(Capture(process, now, "visible", ticks));
                    visibleTicks = hiddenTicks = 0;
                    Console.WriteLine("SOAK: measuring visible widget.");
                }
                if (!hidden && now >= warmupEnd + seconds / 2d)
                {
                    samples.Add(Capture(process, now, "visible", ticks));
                    window.Hide();
                    hidden = true;
                    samples.Add(Capture(process, now, "hidden", ticks));
                    Console.WriteLine("SOAK: measuring hidden widget; monitoring remains active.");
                }
                if (now >= measurementEnd && !stressStarted)
                {
                    samples.Add(Capture(process, now, "hidden", ticks));
                    stressStarted = true;
                    Console.WriteLine("SOAK: beginning repeated sampling, menu, language, theme, show/hide and tray-disposal stress.");
                    app.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(StressBatch));
                }
                else if (!stressStarted && (samples.Count == 0 || now - samples[^1].Seconds >= 5))
                    samples.Add(Capture(process, now, hidden ? "hidden" : "visible", ticks));
            };
            sampler.Start();
        }));

        int exit = app.Run();
        try
        {
            if (exit != 0) errors.Add($"Application exit code: {exit}");
            if (monitor is not { IsEnabled: false }) errors.Add("Sampling timer was not stopped on exit.");
            if (visibleTicks < seconds * .35 || hiddenTicks < seconds * .35) errors.Add("Monitoring lost too many one-second samples in visible/hidden mode.");
            if (maxDelay > 5) errors.Add($"UI heartbeat delayed by {maxDelay:F2} s.");
            if (retainedBefore == null || retainedAfter == null) errors.Add("Retention snapshots were not completed.");
            else
            {
                if (retainedAfter.ManagedBytes - retainedBefore.ManagedBytes > 16 * MiB) errors.Add("Retained managed memory grew by over 16 MiB.");
                if (retainedAfter.PrivateBytes - retainedBefore.PrivateBytes > 48 * MiB) errors.Add("Private memory grew by over 48 MiB after warmup/stress.");
                if (retainedAfter.Handles - retainedBefore.Handles > 80) errors.Add("Process handles grew by over 80.");
                if (retainedAfter.GdiObjects - retainedBefore.GdiObjects > 30) errors.Add("GDI objects grew by over 30.");
                if (retainedAfter.UserObjects - retainedBefore.UserObjects > 30) errors.Add("USER objects grew by over 30.");
            }
            var report = new
            {
                CreatedUtc = DateTimeOffset.UtcNow, Environment.OSVersion, Runtime = Environment.Version.ToString(),
                LogicalProcessors = Environment.ProcessorCount, RequestedSeconds = seconds,
                WallClockSeconds = watch.Elapsed.TotalSeconds, StressIterations = completedIterations,
                StressSeconds = stressDuration.TotalSeconds, VisibleTicks = visibleTicks, HiddenTicks = hiddenTicks,
                MaximumHeartbeatDelaySeconds = maxDelay,
                Visible = Summarize(samples, "visible"), Hidden = Summarize(samples, "hidden"),
                RetainedBefore = retainedBefore, RetainedAfter = retainedAfter, Samples = samples, Errors = errors,
                Passed = errors.Count == 0
            };
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(new { report.Visible, report.Hidden, report.StressIterations, report.StressSeconds, report.Errors }));
            Console.WriteLine($"{(report.Passed ? "PASS" : "FAIL")}: stability report: {reportPath}");
            return report.Passed ? 0 : 1;
        }
        finally
        {
            File.Delete(Path.Combine(directory, "settings.json"));
            File.Delete(Path.Combine(directory, "usage.json"));
            Directory.Delete(directory);
        }
    }

    private static void ExerciseChartDetails(HistoryWindow history)
    {
        history.UpdateLayout();
        var chart = (DailyUsageChart)typeof(HistoryWindow).GetField("_dailyChart", Private)!.GetValue(history)!;
        chart.SetHoverPosition(new Point(chart.ActualWidth / 2, 40));
        var tip = (ToolTip)chart.ToolTip;
        tip.Opacity = 0; tip.IsOpen = true;
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static Sample Capture(Process process, double seconds, string phase, int ticks)
    {
        process.Refresh();
        var heap = GC.GetGCMemoryInfo();
        return new(seconds, phase, ticks, process.TotalProcessorTime.TotalSeconds,
            process.WorkingSet64, process.PrivateMemorySize64, GC.GetTotalMemory(false), GC.GetTotalAllocatedBytes(),
            process.HandleCount, checked((int)GetGuiResources(process.Handle, 0)), checked((int)GetGuiResources(process.Handle, 1)),
            heap.TotalCommittedBytes, heap.FragmentedBytes);
    }

    private static object Summarize(List<Sample> samples, string phase)
    {
        var group = samples.Where(sample => sample.Phase == phase).ToArray();
        if (group.Length < 2) return new { Seconds = 0d };
        var first = group[0];
        var last = group[^1];
        double elapsed = last.Seconds - first.Seconds;
        double cpu = (last.CpuSeconds - first.CpuSeconds) / elapsed * 100;
        return new
        {
            Seconds = elapsed, CpuPercentOfOneCore = cpu, CpuPercentOfMachine = cpu / Environment.ProcessorCount,
            MeanWorkingSetMiB = group.Average(sample => sample.WorkingSetBytes) / MiB,
            PeakWorkingSetMiB = group.Max(sample => sample.WorkingSetBytes) / (double)MiB,
            PrivateGrowthMiB = (last.PrivateBytes - first.PrivateBytes) / (double)MiB,
            AllocatedKiBPerSecond = (last.AllocatedBytes - first.AllocatedBytes) / elapsed / 1024,
            HandleGrowth = last.Handles - first.Handles, GdiGrowth = last.GdiObjects - first.GdiObjects,
            UserGrowth = last.UserObjects - first.UserObjects
        };
    }

    private sealed record Sample(double Seconds, string Phase, int Ticks, double CpuSeconds,
        long WorkingSetBytes, long PrivateBytes, long ManagedBytes, long AllocatedBytes, int Handles, int GdiObjects, int UserObjects,
        long HeapCommittedBytes, long HeapFragmentedBytes);

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process, uint flags);
}
