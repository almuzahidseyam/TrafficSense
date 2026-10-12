using System.IO;
using System.Security;

namespace TrafficSense;

internal interface IStartupRegistration
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}

internal readonly record struct StartupStatus(bool? Enabled, string? Error = null);

internal sealed class StartupService(IStartupRegistration registration)
{
    public StartupStatus Read()
    {
        try { return new(registration.IsEnabled); }
        catch (Exception error) when (IsRegistrationError(error))
        {
            return new(null, $"Windows startup settings could not be read. {error.Message}");
        }
    }

    public StartupStatus SetEnabled(bool enabled)
    {
        try
        {
            registration.SetEnabled(enabled);
            var status = Read();
            return status.Enabled == enabled || status.Error != null
                ? status
                : status with { Error = "Windows did not apply the startup setting. Please try again." };
        }
        catch (Exception error) when (IsRegistrationError(error))
        {
            return Read() with { Error = $"Windows startup settings could not be changed. {error.Message}" };
        }
    }

    private static bool IsRegistrationError(Exception error) =>
        error is IOException or UnauthorizedAccessException or SecurityException or InvalidOperationException;
}
