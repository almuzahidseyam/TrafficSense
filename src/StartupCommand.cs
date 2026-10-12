using System.IO;

namespace TrafficSense;

internal static class StartupCommand
{
    public static string Build(string? processPath, string? assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
            throw new InvalidOperationException("The application executable could not be located.");

        if (!string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            return $"\"{processPath}\"";

        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new InvalidOperationException("The application DLL could not be located.");

        return $"\"{processPath}\" \"{assemblyPath}\"";
    }
}
