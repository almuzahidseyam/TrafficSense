using System.Windows;

// An x64 framework-dependent apphost uses the same runtime discovery as Lite.
// No application settings, windows, network requests or system changes.
if (!Environment.Is64BitProcess || Environment.Version.Major != 8) return 1;
if (typeof(Window).Assembly.GetName().Version?.Major != 8) return 2;
Console.WriteLine("TrafficSense .NET 8 Desktop x64: ready");
return 0;
