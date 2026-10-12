using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TrafficSense;
using Forms = System.Windows.Forms;

internal static class BackupProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static int Run()
    {
        string parent = Path.Combine(Path.GetTempPath(), "TrafficSense.BackupLifecycle");
        string root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json"), backup = Path.Combine(root, "backup.json");
        var store = new SettingsStore(settingsPath);
        Check(store.Save(new AppSettings()).Success, "Prepare settings.");
        var errors = new List<string>();
        bool allowShortcut = true;
        var shortcut = new GlobalShortcut(_ => allowShortcut, _ => { });
        var app = new App(@"Local\TrafficSense.Backup." + Guid.NewGuid().ToString("N"), () =>
            new MainWindow(store.Load(), store.Save, errors.Add, readAdapters: () => [], shortcut: shortcut));
        app.InitializeComponent();
        var style = new Style(typeof(MainWindow));
        style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
        style.Setters.Add(new Setter(Window.ShowActivatedProperty, false));
        app.Resources.Add(typeof(MainWindow), style);
        bool passed = false;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                var window = (MainWindow)app.MainWindow;
                Drain(window);
                var tray = new TrayController(window);
                try
                {
                    window.Left = 150; window.Top = 120; Drain(window);
                    string savedBefore = File.ReadAllText(settingsPath);
                    Check(window.ExportSettings(backup), "Export failed.");
                    Check(File.ReadAllText(settingsPath) == savedBefore, "Export changed saved settings.");
                    var exported = SettingsBackup.Read(backup);
                    Check(exported.ScreenLeft.HasValue && exported.WindowLeft == window.Left, "Export missed current position.");

                    var desired = new AppSettings
                    {
                        Language = "bn", Theme = "Light", WidgetFontSize = 24, BackgroundOpacity = 64,
                        SpeedInBits = true, RefreshMilliseconds = 500, PositionLocked = true,
                        GlobalShortcutEnabled = true, SelectedAdapterId = "other-pc",
                        ScreenLeft = int.MaxValue, ScreenTop = int.MaxValue
                    };
                    Check(SettingsBackup.Export(backup, desired).Success, "Prepare backup.");
                    window.Hide(); Drain(window);
                    Check(window.RestoreSettings(backup), "Hidden restore failed.");
                    Check(!window.IsVisible && shortcut.Enabled && window.PositionLocked && window.Localization.Code == "bn", "Restore visibility/preferences.");
                    var saved = store.Load();
                    Check(saved.SelectedAdapterId == null && saved.SpeedInBits && saved.WidgetFontSize == 24 && saved.BackgroundOpacity == 64,
                        "Missing adapter fallback or saved preferences failed.");
                    Check(((DispatcherTimer)Field(window, "_timer")).Interval == TimeSpan.FromMilliseconds(500), "Restore timer.");
                    Check(((MenuItem)window.FindName("MenuBackup")).Header.ToString() == "সেটিংস সংরক্ষণ করুন…", "Restore menu language.");
                    var menu = (Forms.ContextMenuStrip)typeof(TrayController).GetField("_menu", Private)!.GetValue(tray)!;
                    Check(menu.Items.OfType<Forms.ToolStripMenuItem>().Single(item => Equals(item.Tag, "position-lock")).Checked,
                        "Restore did not synchronize tray lock.");
                    Check((bool)Field(window, "_restorePlacementPending"), "Hidden restore moved/resized the widget prematurely.");
                    Check(((UsageLedger)Field(window, "_usage")).Today(new(2026, 10, 12)).Received == 0, "Restore changed usage.");

                    window.Show(); Drain(window);
                    Check(!(bool)Field(window, "_restorePlacementPending"), "Visible restore did not place widget.");
                    Check(((TextBlock)window.FindName("TxtDownload")).FontSize == 24 &&
                        ((SolidColorBrush)((Border)window.Content).Background).Color == Color.FromArgb(64, 255, 255, 255), "Appearance not applied.");
                    var placed = new AppSettings(); NativeWindowPlacement.Capture(window, placed);
                    Check(placed.ScreenLeft != int.MaxValue && placed.ScreenTop != int.MaxValue, "Missing monitor not recovered.");
                    Check(((TextBlock)window.FindName("TxtDownload")).Text.EndsWith("bps"), "Unit not rendered.");

                    string current = File.ReadAllText(settingsPath);
                    File.WriteAllText(backup, "{}");
                    Check(!window.RestoreSettings(backup) && File.ReadAllText(settingsPath) == current && shortcut.Enabled && window.Localization.Code == "bn",
                        "Invalid backup changed current settings.");
                    desired.Language = "en"; desired.GlobalShortcutEnabled = false;
                    Check(SettingsBackup.Export(backup, desired).Success, "Prepare rollback.");
                    using (var locked = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.None))
                        Check(!window.RestoreSettings(backup), "Blocked save succeeded.");
                    Check(File.ReadAllText(settingsPath) == current && shortcut.Enabled && window.Localization.Code == "bn", "Save failure changed state.");
                    Check(window.ChangeGlobalShortcut(false), "Disable shortcut.");
                    desired.GlobalShortcutEnabled = true; allowShortcut = false;
                    Check(SettingsBackup.Export(backup, desired).Success, "Prepare conflict.");
                    current = File.ReadAllText(settingsPath);
                    Check(!window.RestoreSettings(backup) && File.ReadAllText(settingsPath) == current && !shortcut.Enabled, "Conflict changed settings.");

                    // A newly acquired shortcut must be released if the settings write fails.
                    allowShortcut = true;
                    using (var locked = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.None))
                        Check(!window.RestoreSettings(backup), "Blocked enable-save succeeded.");
                    Check(!shortcut.Enabled && window.Localization.Code == "bn", "Failed restore retained new shortcut.");

                    desired.GlobalShortcutEnabled = false; desired.ScreenLeft = 300; desired.ScreenTop = 250;
                    Check(SettingsBackup.Export(backup, desired).Success, "Prepare hidden exit.");
                    window.Hide(); Drain(window);
                    Check(window.RestoreSettings(backup), "Restore before hidden exit.");
                    Check(window.ExportSettings(backup) && SettingsBackup.Read(backup).ScreenLeft == 300, "Re-export lost pending position.");
                    Check(errors.Count == 4, "Unexpected restore errors.");
                    tray.Dispose(); window.Close();
                    saved = store.Load();
                    Check(saved.ScreenLeft == 300 && saved.ScreenTop == 250 && saved.Language == "en", "Hidden exit lost restored position.");
                    passed = true;
                }
                finally { tray.Dispose(); }
            }
            catch (Exception error) { Console.Error.WriteLine(error); app.Shutdown(1); }
        }));
        try
        {
            Check(app.Run() == 0 && passed, "Backup lifecycle failed.");
            Console.WriteLine("PASS: backup capture, bilingual live/hidden restore, missing adapter/monitor recovery, tray/timer/appearance sync, corrupt/save/conflict rollback, shortcut cleanup and hidden-exit persistence.");
            return 0;
        }
        finally
        {
            if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(parent) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(root, true);
        }
    }
    private static object Field(MainWindow window, string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window)!;
    private static void Drain(Window window) { window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
