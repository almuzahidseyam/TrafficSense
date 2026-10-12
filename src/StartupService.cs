using System.IO;
using System.Security;

namespace TrafficSense;

internal interface IStartupRegistration
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}

internal enum StartupError { ReadFailed, ChangeFailed, NotApplied }

internal readonly record struct StartupStatus(bool? Enabled, string? Error = null, StartupError? Failure = null);

internal sealed class StartupService(IStartupRegistration registration)
{
    public StartupStatus Read()
    {
        try { return new(registration.IsEnabled); }
        catch (Exception error) when (IsRegistrationError(error))
        {
            return new(null, error.Message, StartupError.ReadFailed);
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
                : status with { Error = "", Failure = StartupError.NotApplied };
        }
        catch (Exception error) when (IsRegistrationError(error))
        {
            return Read() with { Error = error.Message, Failure = StartupError.ChangeFailed };
        }
    }

    private static bool IsRegistrationError(Exception error) =>
        error is IOException or UnauthorizedAccessException or SecurityException or InvalidOperationException;
}
