using Microsoft.Win32;
using System.IO;

namespace TrafficSense;

internal sealed class WindowsStartupRegistration(
    string runKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
    string valueName = "TrafficSense") : IStartupRegistration
{
    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKeyPath);
            return key?.GetValue(valueName) is string command &&
                string.Equals(command, CurrentCommand(), StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(runKeyPath, writable: true)
                ?? throw new InvalidOperationException("The Windows startup registry key could not be opened.");
            key.SetValue(valueName, CurrentCommand(), RegistryValueKind.String);
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    private static string CurrentCommand() => StartupCommand.Build(
        Environment.ProcessPath, Path.Combine(AppContext.BaseDirectory,
            $"{typeof(WindowsStartupRegistration).Assembly.GetName().Name}.dll"));
}
