using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using TrafficSense;
using Forms = System.Windows.Forms;

internal static class AboutProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static int Run(bool fatal)
    {
        string root = Path.Combine(Path.GetTempPath(), "TrafficSense.AboutTests", Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "logs with spaces বাংলা");
        var diagnostics = new DiagnosticLog(folder);
        var opened = new List<string>();
        var errors = new List<string>();
        bool failLaunch = false, passed = false;
        AboutWindow? last = null;
        var app = new App(@"Local\TrafficSense.About." + Guid.NewGuid().ToString("N"),
            () => new MainWindow(new AppSettings(), _ => new SettingsSaveResult(true), errors.Add,
                diagnostics: diagnostics, launchLogFolder: path =>
                {
                    Check(Directory.Exists(path), "Folder was not created before launch.");
                    if (failLaunch) throw new Win32Exception("Simulated folder launch failure.");
                    opened.Add(path);
                }));
        app.InitializeComponent();
        foreach (Type type in new[] { typeof(MainWindow), typeof(AboutWindow) })
        {
            var style = new Style(type);
            style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
            style.Setters.Add(new Setter(Window.ShowActivatedProperty, false));
            app.Resources.Add(type, style);
        }
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var window = (MainWindow)app.MainWindow;
                var tray = typeof(App).GetField("_tray", Private)!.GetValue(app)!;
                var notify = (Forms.NotifyIcon)tray.GetType().GetField("_notifyIcon", Private)!.GetValue(tray)!;
                var trayAbout = notify.ContextMenuStrip!.Items.OfType<Forms.ToolStripMenuItem>().Single(item => item.Tag as string == "about");
                var widgetAbout = (MenuItem)window.FindName("MenuAbout");
                typeof(MenuItem).GetMethod("OnClick", Private)!.Invoke(widgetAbout, null);
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
                var about = app.Windows.OfType<AboutWindow>().Single();
                Check(window.Icon != null && about.Icon != null &&
                    ((Image)about.FindName("BrandLogo")).Source is System.Windows.Media.Imaging.BitmapSource { PixelWidth: 128 },
                    "Application branding resources did not load.");
                Check(notify.Icon != null, "Tray logo is missing.");
                VerifyBrandColors(notify.Icon!);
                string? packaged = Environment.GetEnvironmentVariable("TRAFFICSENSE_PACKAGE_EXE");
                if (!string.IsNullOrEmpty(packaged))
                {
                    using var extracted = System.Drawing.Icon.ExtractAssociatedIcon(packaged);
                    Check(extracted != null, "Published executable icon is missing.");
                    VerifyBrandColors(extracted!);
                }
                Check(about.IsVisible && window.IsVisible && trayAbout.Text == "About TrafficSense", "Widget About did not open.");
                string version = Text(about, "VersionValue");
                Check(version == typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
                    "About version does not match the running application assembly.");
                Check(Text(about, "LogFolderValue") == folder && !Directory.Exists(folder), "About changed or created the log folder on display.");
                Capture(about, "en");
                Check(window.ChangeLanguage("bn"), "Could not change language.");
                Check(about.Title == "TrafficSense সম্পর্কে" && trayAbout.Text == about.Title && widgetAbout.Header.ToString() == about.Title,
                    "About menu and title did not translate live.");
                Check(((TextBlock)about.FindName("VersionLabel")).Text == "সংস্করণ" &&
                    ((Button)about.FindName("OpenLogFolderButton")).Content.ToString() == "লগ ফোল্ডার খুলুন", "About controls did not translate.");
                Capture(about, "bn");
                Click(about, "OpenLogFolderButton");
                Check(opened.SequenceEqual(new[] { folder }) && !File.Exists(diagnostics.FilePath), "Fresh-folder opening changed its path or generated a false error log.");
                failLaunch = true;
                Click(about, "OpenLogFolderButton");
                Check(errors.Count == 1 && errors[0].StartsWith("লগ ফোল্ডার খোলা যায়নি।") &&
                    File.ReadAllText(diagnostics.FilePath).Contains("OpenLogFolder"), "Launch failure was not localized and logged.");
                Check(about.IsVisible && window.IsEnabled, "Recoverable failure closed or disabled the app.");
                failLaunch = false;
                Click(about, "OpenLogFolderButton");
                Check(opened.Count == 2, "Folder opening did not recover after failure.");
                window.Hide();
                trayAbout.PerformClick();
                Check(!window.IsVisible && about.IsVisible && app.Windows.OfType<AboutWindow>().Single() == about,
                    "Hidden-widget tray access showed the widget or duplicated About.");
                about.WindowState = WindowState.Minimized;
                trayAbout.PerformClick();
                Check(about.WindowState == WindowState.Normal && !window.IsVisible, "Repeated About did not restore the existing window.");
                Click(about, "CloseButton");
                Check(!app.Windows.OfType<AboutWindow>().Any() && !window.IsVisible, "Close did not release About or showed the widget.");
                window.ChangeLanguage("en");
                Check(about.Title == "TrafficSense সম্পর্কে", "Closed About retained its language subscription.");
                trayAbout.PerformClick();
                last = app.Windows.OfType<AboutWindow>().Single();
                Check(last != about && last.Title == "About TrafficSense" && Text(last, "VersionValue") == version && !window.IsVisible,
                    "Reopening hidden About did not use current language/version.");
                string blockedPath = Path.Combine(root, "blocked folder");
                File.WriteAllText(blockedPath, "preserve");
                var blocked = new AboutWindow(window.Localization, new DiagnosticLog(blockedPath), errors.Add,
                    _ => throw new InvalidOperationException("Launcher must not run when creation fails."));
                blocked.Show();
                Click(blocked, "OpenLogFolderButton");
                Check(errors.Count == 2 && errors[1].StartsWith("The log folder could not be opened.") &&
                    File.ReadAllText(blockedPath) == "preserve" && blocked.IsVisible, "Blocked folder was not handled safely.");
                blocked.Close();
                if (fatal)
                {
                    window.PrepareForFatalShutdown();
                    Check(!last.IsVisible, "Fatal shutdown left About interactive.");
                    window.ShowAbout();
                    Check(!app.Windows.OfType<AboutWindow>().Any(), "About reopened during fatal shutdown.");
                }
                passed = true;
                window.Close();
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                app.Shutdown(1);
            }
        }));
        try
        {
            int exit = app.Run();
            Check(exit == 0 && passed && last is { IsVisible: false }, "About lifecycle or shutdown failed.");
            Console.WriteLine($"PASS: About version, widget/tray access, live English/Bangla, hidden/minimized reuse, folder creation/launch errors and {(fatal ? "fatal" : "normal")} cleanup.");
            return 0;
        }
        finally
        {
            string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TrafficSense.AboutTests")) + Path.DirectorySeparatorChar;
            Check(Path.GetFullPath(root).StartsWith(parent, StringComparison.OrdinalIgnoreCase), "Unsafe temporary cleanup path.");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string Text(AboutWindow window, string name) => ((TextBox)window.FindName(name)).Text;
    private static void VerifyBrandColors(System.Drawing.Icon icon)
    {
        using var bitmap = icon.ToBitmap();
        int cyan = 0, mint = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A > 200 && pixel.R < 100 && pixel.G > 150 && pixel.B > 200) cyan++;
                if (pixel.A > 200 && pixel.R < 130 && pixel.G > 180 && pixel.B is > 90 and < 200) mint++;
            }
        Check(cyan > 2 && mint > 2, "Expected download/upload logo colors are absent from the Windows icon.");
    }
    internal static void Capture(AboutWindow window, string language)
    {
        string? directory = Environment.GetEnvironmentVariable("TRAFFICSENSE_ABOUT_SCREENSHOTS");
        if (string.IsNullOrEmpty(directory)) return;
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        window.UpdateLayout();
        var content = (FrameworkElement)window.Content;
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),
            (int)Math.Ceiling(content.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        var visual = new System.Windows.Media.DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            var bounds = new Rect(0, 0, content.ActualWidth, content.ActualHeight);
            drawing.DrawRectangle(window.Background, null, bounds);
            drawing.DrawRectangle(new System.Windows.Media.VisualBrush(content) { Stretch = System.Windows.Media.Stretch.None }, null, bounds);
        }
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, $"about-{language}.png"));
        encoder.Save(stream);
    }
    private static void Click(AboutWindow window, string name) => ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
