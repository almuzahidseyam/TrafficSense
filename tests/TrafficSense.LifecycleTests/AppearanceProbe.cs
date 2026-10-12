using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TrafficSense;
using Forms = System.Windows.Forms;

internal static class AppearanceProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "TrafficSense.AppearanceTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "settings.json");
        var store = new SettingsStore(path);
        Check(store.Save(new AppSettings { SelectedAdapterId = "appearance-manual" }).Success, "Could not create isolated settings.");
        var errors = new List<string>();
        int saves = 0;
        var app = CreateApp(() => new MainWindow(store.Load(), settings => { saves++; return store.Save(settings); },
            errors.Add, readAdapters: () => [], sampleTimestamp: () => TimeSpan.FromSeconds(1)));
        bool passed = false;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var window = (MainWindow)app.MainWindow;
                Drain(window);
                CheckStyle(window, 14, 120);
                var calculator = typeof(MainWindow).GetField("_speedCalculator", Private)!.GetValue(window)!;
                var baselineField = calculator.GetType().GetField("_previousTimestamp", Private)!;
                object? baseline = baselineField.GetValue(calculator);
                double originalWidth = window.ActualWidth;
                var handle = new WindowInteropHelper(window).Handle;
                var work = Forms.Screen.FromHandle(handle).WorkingArea;
                Check(GetWindowRect(handle, out var initial), "Could not read test bounds.");
                Check(SetWindowPos(handle, IntPtr.Zero, work.Right - (initial.Right - initial.Left),
                    work.Bottom - (initial.Bottom - initial.Top), 0, 0, 0x0001 | 0x0004 | 0x0010), "Could not position test widget.");
                Click(Option(window, "MenuFontSize", 24));
                Drain(window);
                CheckStyle(window, 24, 120);
                Check(window.ActualWidth > originalWidth, "Larger text did not resize the widget.");
                CheckInsideWorkArea(window);
                Check(store.Load().WidgetFontSize == 24, "Text size was not saved.");
                int unchangedSaves = saves;
                Click(Option(window, "MenuFontSize", 24));
                Check(saves == unchangedSaves && Option(window, "MenuFontSize", 24).IsChecked,
                    "Re-selecting a checked size wrote settings or unchecked the selection.");
                foreach (int opacity in WidgetAppearance.BackgroundOpacities)
                {
                    Click(Option(window, "MenuTransparency", opacity));
                    CheckStyle(window, 24, opacity);
                    Check(store.Load().BackgroundOpacity == opacity, "Transparency was not saved.");
                }
                Click(Option(window, "MenuTransparency", 64));
                Click((MenuItem)window.FindName("MenuTheme"));
                CheckStyle(window, 24, 64);
                Check(((SolidColorBrush)((Border)window.Content).Background).Color.R == 255 &&
                    ((SolidColorBrush)((TextBlock)window.FindName("TxtDownload")).Foreground).Color == Colors.Black,
                    "Light theme did not preserve transparency and readable text.");
                window.ChangeLanguage("bn");
                Check(((MenuItem)window.FindName("MenuFontSize")).Header.ToString() == "লেখার আকার" &&
                    ((MenuItem)window.FindName("MenuTransparency")).Header.ToString() == "পটভূমির স্বচ্ছতা" &&
                    Option(window, "MenuTransparency", 120).Header.ToString() == "ডিফল্ট", "Appearance menus did not translate.");
                byte[] saved = File.ReadAllBytes(path);
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    Click(Option(window, "MenuFontSize", 12));
                    Click(Option(window, "MenuTransparency", 255));
                }
                CheckStyle(window, 24, 64);
                Check(errors.Count == 2 && errors.All(message => message.StartsWith("সেটিং সংরক্ষণ করা যায়নি।")) &&
                    saved.SequenceEqual(File.ReadAllBytes(path)), "Failed save did not roll back style/checks or preserve settings.");
                Check(Equals(baseline, baselineField.GetValue(calculator)) && store.Load().SelectedAdapterId == "appearance-manual",
                    "Appearance change reset the sampling baseline or changed the adapter.");
                window.Hide();
                Click(Option(window, "MenuFontSize", 16));
                Check(!window.IsVisible && ((DispatcherTimer)typeof(MainWindow).GetField("_timer", Private)!.GetValue(window)!).IsEnabled,
                    "Hidden appearance change showed the widget or stopped monitoring.");
                window.Show();
                Drain(window);
                CheckStyle(window, 16, 64);
                CheckInsideWorkArea(window);
                Click(Option(window, "MenuFontSize", 18));
                Capture(window);
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
            Check(passed && exit == 0, "Appearance lifecycle failed.");
            Program.RunChild("--appearance-restart", path);
            Console.WriteLine("PASS: live text/icon sizing, background-only transparency, theme/language persistence, save rollback, hidden monitoring, edge placement and second-process restart.");
            return 0;
        }
        finally
        {
            string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TrafficSense.AppearanceTests")) + Path.DirectorySeparatorChar;
            Check(Path.GetFullPath(root).StartsWith(parent, StringComparison.OrdinalIgnoreCase), "Unsafe temporary cleanup path.");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    internal static int VerifyRestart(string path)
    {
        var store = new SettingsStore(path);
        var app = CreateApp(() => new MainWindow(store.Load(), store.Save, readAdapters: () => []));
        bool passed = false;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var window = (MainWindow)app.MainWindow;
                CheckStyle(window, 18, 64);
                Check(window.Localization.Code == "bn" && store.Load().Theme == "Light" && store.Load().SelectedAdapterId == "appearance-manual",
                    "Restart lost saved preferences.");
                passed = true;
                window.Close();
            }
            catch (Exception error) { Console.Error.WriteLine(error); app.Shutdown(1); }
        }));
        int exit = app.Run();
        Check(passed && exit == 0, "Appearance restart failed.");
        return 0;
    }

    private static App CreateApp(Func<MainWindow> createWindow)
    {
        var app = new App(@"Local\TrafficSense.Appearance." + Guid.NewGuid().ToString("N"), createWindow);
        app.InitializeComponent();
        var style = new Style(typeof(MainWindow));
        style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
        style.Setters.Add(new Setter(Window.ShowActivatedProperty, false));
        app.Resources.Add(typeof(MainWindow), style);
        return app;
    }

    private static MenuItem Option(MainWindow window, string name, int value) =>
        ((MenuItem)window.FindName(name)).Items.OfType<MenuItem>().Single(item => (int)item.Tag == value);
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
    private static void CheckStyle(MainWindow window, int size, int alpha)
    {
        Check(((TextBlock)window.FindName("TxtDownload")).FontSize == size && ((TextBlock)window.FindName("TxtUpload")).FontSize == size &&
            ((Wpf.Ui.Controls.SymbolIcon)window.FindName("IconDownload")).FontSize == size + 2 &&
            ((Wpf.Ui.Controls.SymbolIcon)window.FindName("IconUpload")).FontSize == size + 2,
            "Text/icon size did not update.");
        Check(((SolidColorBrush)((Border)window.Content).Background).Color.A == alpha && window.Opacity == 0 &&
            ((TextBlock)window.FindName("TxtDownload")).Opacity == 1, "Transparency affected text or the whole window.");
        Check(((MenuItem)window.FindName("MenuFontSize")).Items.OfType<MenuItem>().Count(item => item.IsChecked) == 1 &&
            Option(window, "MenuFontSize", size).IsChecked &&
            ((MenuItem)window.FindName("MenuTransparency")).Items.OfType<MenuItem>().Count(item => item.IsChecked) == 1 &&
            Option(window, "MenuTransparency", alpha).IsChecked, "Appearance checkmarks are incorrect.");
    }
    private static void CheckInsideWorkArea(MainWindow window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var work = Forms.Screen.FromHandle(handle).WorkingArea;
        Check(GetWindowRect(handle, out var bounds) && bounds.Left >= work.Left && bounds.Top >= work.Top &&
            bounds.Right <= work.Right && bounds.Bottom <= work.Bottom, "Resized widget was left outside the work area.");
    }
    private static void Capture(MainWindow window)
    {
        string? directory = Environment.GetEnvironmentVariable("TRAFFICSENSE_APPEARANCE_SCREENSHOTS");
        if (string.IsNullOrEmpty(directory)) return;
        Drain(window);
        var content = (FrameworkElement)window.Content;
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),
            (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, "appearance.png"));
        encoder.Save(stream);
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
}
