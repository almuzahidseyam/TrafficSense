using System.IO;

namespace TrafficSense;

public static class SettingsManager
{
    private static readonly SettingsStore Store = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrafficSense", "settings.json"), DiagnosticLog.Default);
    private static readonly StartupService Startup = new(new WindowsStartupRegistration());

    public static AppSettings Load() => Store.Load();
    public static SettingsSaveResult Save(AppSettings settings) => Store.Save(settings);
    internal static StartupStatus GetAutoStart() => Startup.Read();
    internal static StartupStatus SetAutoStart(bool enabled) => Startup.SetEnabled(enabled);
}
