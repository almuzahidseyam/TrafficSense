TrafficSense - Windows x64 portable edition

1. Extract the entire ZIP to a folder you can keep.
2. Exit any older TrafficSense instance from its tray menu (Hide is not Exit).
3. Open TrafficSense.exe. No separate .NET installation is needed.

Supports 64-bit Windows 10/11. This package includes the .NET 8 runtime.
See RELEASE-NOTES.md for this version's changes and upgrade instructions.
CHANGELOG.md contains the release history and publication status.
The first launch extracts native runtime files to your Windows temporary folder.
Keep the license and notice files with the application when redistributing it.

Right-click the widget or tray icon to select English / বাংলা, change the theme,
choose a network adapter, show/hide the widget, or exit.

Settings are stored in %APPDATA%\TrafficSense\settings.json, not beside the EXE.
This is a portable application, not a portable user-settings directory.
Right-click for Lock position, Reset position, Usage and history, speed units,
refresh interval, or optional Ctrl+Alt+T show/hide. With the widget focused:
Ctrl+L lock, Ctrl+R reset, Ctrl+H history, F1 About, Escape hide, Shift+F10 menu,
arrow keys move (Shift for fine movement). Lock blocks keyboard movement too.
Usage totals cover valid intervals while the app runs; sleep/reconnect/app-off
gaps are excluded. They are not ISP billing totals. Data is saved every minute
and on normal exit to %LOCALAPPDATA%\TrafficSense\usage.json (latest 400 days).
The graph shows two minutes of in-memory speed history with solid download and
dashed upload lines. A hard shutdown can lose the last unsaved minute of usage.

An optional TrafficSense-Setup.exe is available alongside the ZIP on GitHub.
It installs per user; uninstall through Windows Installed apps. Settings,
usage and logs are retained. Exit TrafficSense before upgrading/uninstalling.
Error logs are stored in %LOCALAPPDATA%\TrafficSense\logs\errors.jsonl.
Choose About TrafficSense from the widget or tray menu to check the app version
and open the log folder. The address can also be copied from the About window.
Two older logs are retained; no logs are uploaded automatically. On an unexpected
UI/startup failure, the app saves diagnostics when possible and closes. Start it
again to resume monitoring. Review logs before including them in an issue report.
If Start with Windows was enabled before moving the app, enable it again from
the new location. Only one instance runs per user in the current sign-in session.

Source, updates and issue reports:
https://github.com/almuzahidseyam/TrafficSense
