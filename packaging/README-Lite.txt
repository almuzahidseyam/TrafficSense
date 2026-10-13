TrafficSense Lite - Windows x64 portable edition

Same TrafficSense features, smaller download: this package omits .NET.
Requires Microsoft .NET 8 Desktop Runtime (x64), with the latest 8.0 patch.
The Windows .NET 8 x64 SDK also includes that runtime. .NET Framework 4.x,
the plain .NET Runtime, x86-only runtimes, and .NET 9/10 alone are insufficient.

1. Extract the entire ZIP to a folder you can keep.
2. Exit the older TrafficSense instance from its tray menu and close its Sensor
   bridge. Hide is not Exit. Open TrafficSense.exe.
3. If Windows asks to install .NET, install the .NET 8 Desktop Runtime x64 from
   https://dotnet.microsoft.com/en-us/download/dotnet/8.0
   Or use the full TrafficSense.zip / TrafficSense-Setup.exe instead.

No features, settings, usage history or plugin support are removed in Lite.
Settings/history/log locations and single-instance behavior are shared with
the full edition. Changing edition does not create a second running widget.
After moving the app, enable Start with Windows again from its new location.
The shared runtime is maintained through your .NET installation, separately
from TrafficSense updates. Keep the license files when redistributing.

See RELEASE-NOTES.md and CHANGELOG.md for this version's changes.
English and Bangla instructions, measurement limits, troubleshooting:
https://github.com/almuzahidseyam/TrafficSense#readme
