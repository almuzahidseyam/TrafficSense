# TrafficSense 🚀 

**A Modern, Lightweight, and Premium Network Traffic Monitor for Windows.**

TrafficSense is a lightweight real-time network speed monitor for Windows. Built with C# and WPF, it provides a compact draggable widget, light/dark themes, system tray controls, and English/Bengali menus and app messages.

> **Note:** A screenshot of the app will be added here soon!

## 🌟 Features

- **Real-Time Speed:** Accurate upload and download speed tracking.
- **Compact Widget:** Transparent, always-on-top widget with light/dark themes.
- **Language Support:** Switch between English and Bengali (বাংলা) without restarting.
- **Lightweight:** Uses minimal RAM and CPU.
- **Portable:** Extract the ZIP and run the executable.
- **Customizable:** Choose the language, theme (Light/Dark), network adapter, and widget position.
- **Open Source:** 100% free and open-source.

## 📥 Download & Installation

Version **[1.18.0](https://github.com/almuzahidseyam/TrafficSense/releases/tag/v1.18.0)**. See [CHANGELOG.md](CHANGELOG.md) for the changes and upgrade instructions.

1. Go to the [Releases](https://github.com/almuzahidseyam/TrafficSense/releases) page.
2. Download `TrafficSense-Setup.exe` for installation, or `TrafficSense.zip` for the portable edition.
3. Run Setup to install for your Windows account (no administrator rights required), or extract the entire ZIP and run `TrafficSense.exe` on **64-bit Windows 10/11**. Both packages include .NET 8; releases before 1.3.0 may require a separate .NET 8 Desktop Runtime installation.

Setup creates Start menu shortcuts and an optional desktop shortcut. Remove it through Windows **Installed apps → TrafficSense → Uninstall**. Exit the widget before upgrading/uninstalling; Setup does not close it for you. Settings, usage history and error logs are retained. Uninstall removes the startup entry only when it points to that installed copy. Portable use remains available without installation.

Exit an older running version from its tray menu before opening the new EXE. Hiding it keeps it running, and a second launch will show that older instance. Settings remain in `%APPDATA%`, and the bundled native runtime is extracted to the Windows temporary folder on first launch.

Releases also include `TrafficSense.zip.sha256` and `TrafficSense-Setup.exe.sha256`. To check a download, compare `Get-FileHash <download> -Algorithm SHA256` with its checksum file.

## 🛠️ Built With

- **C# & .NET 8**
- **WPF (Windows Presentation Foundation)**
- **WPF-UI** (For the modern Windows 11 look)

## 💻 How to Build Locally

If you want to compile the project yourself:

1. Install [.NET 8 SDK](https://dotnet.microsoft.com/download).
2. Clone the repository:
   ```bash
   git clone https://github.com/almuzahidseyam/TrafficSense.git
   ```
3. Navigate to the folder and build:
   ```bash
   cd TrafficSense
   dotnet build src/TrafficSense.csproj -c Release
   ```
4. Run the application:
   ```bash
   dotnet run --project src/TrafficSense.csproj
   ```

5. Run the automated regression tests:
   ```bash
   dotnet test tests/TrafficSense.Tests/TrafficSense.Tests.csproj -c Release
   ```

6. On Windows, verify the real application startup, tray controls, and second-process activation:
   ```bash
   dotnet run --project tests/TrafficSense.LifecycleTests/TrafficSense.LifecycleTests.csproj -c Release
   ```
   This probe uses an invisible window, an isolated instance name, and a temporary settings file. It verifies language switching, persistence/restart, save-failure rollback, tray controls, and process activation without changing user settings or startup registration. A temporary tray icon may appear during the probe.

### Build the portable package

On Windows with the .NET 8 SDK and PowerShell:

```powershell
./scripts/Build-Portable.ps1
# If the SDK is installed at a custom location:
./scripts/Build-Portable.ps1 -DotNet 'C:/path/to/dotnet.exe'
```

The script prints paths to `TrafficSense.zip`, its SHA-256 checksum, and the extracted executable in a fresh `.artifacts/portable/<build-id>` folder. It uses `build/Portable.pubxml` for a self-contained `win-x64` single-file publish with native libraries included and WPF trimming disabled. Runtime notices, the project license, WPF-UI license, and a quick-start guide are included in the ZIP.

Before reporting success, it checks the ZIP's extracted files against the publish output, publishes the lifecycle probe with the same profile, and exercises its real widget/tray/language/restart behavior with installed-runtime lookup disabled. It verifies embedded CoreLib and WPF native libraries loaded from the bundle's extraction directory, rejecting an external CoreCLR if one is loaded (Windows single-file hosts can link CoreCLR internally). When no user widget is running, it also launches the distributed EXE from the extracted ZIP, confirms bundled/internal CoreCLR in the host trace, and checks secondary-instance activation while holding its instance gate so user settings are untouched. That entry-point check is skipped if a user widget already owns the gate; CI runs it on a fresh Windows runner.

CI uploads verified ZIP/installer packages and checksums as `TrafficSense-win-x64`. The release workflow runs the same checks and attaches all four files when a `v*` tag is pushed. Ordinary builds and pull requests do not publish a release. Rebuild packages with an updated SDK/runtime for .NET security servicing; the bundled runtime does not automatically use an installed runtime update.

### Measure CPU, memory and long-running stability

```powershell
./scripts/Test-Stability.ps1 -Seconds 600 -StressIterations 10000
# A one-hour visible/hidden run (plus warmup and stress):
./scripts/Test-Stability.ps1 -Seconds 3600 -StressIterations 10000
```

Use `-DotNet 'C:/path/to/dotnet.exe'` for a custom SDK location. The script publishes the probe with the portable profile and prints a JSON report path in `.artifacts/stability/<run-id>`. It uses an isolated instance gate and temporary settings, without changing the running user's widget or startup registration. A separate test widget and tray icon appear; stress show/hide cycles do not activate the window.

After a 20-second warmup, it measures the real one-second monitor for half the requested duration with the widget shown and half hidden. Reports include process CPU (both one-core and whole-machine percentages), working set, private memory, managed allocations, process/GDI/USER handles, actual sampling ticks and UI heartbeat delays. Working set includes shared runtime pages and is not the same as private memory. The measurements include probe overhead and depend on Windows, hardware, network traffic and whether other windows cover the widget.

The stress phase repeatedly samples live adapters, rebuilds menus, switches language/theme, shows/hides the window, and creates/disposes temporary tray controllers. Stress iterations are accelerated operations, **not equivalent to hours of elapsed time**. After cleanup/GC, the probe checks retained managed/private memory and native resources against generous regression budgets; CPU is recorded rather than gated by a hardware-specific threshold. A bounded parent-process timeout catches a stalled UI. CI runs a shorter 60-second/1,500-iteration check and uploads its JSON report, including on a resource-check failure. Passing a finite run does not guarantee leak-free behavior over days or every Windows/driver combination.

## Settings & Window Position

- Right-click the widget → **Settings backup / সেটিংস ব্যাকআপ** → **Back up settings… / সেটিংস সংরক্ষণ করুন…** to save a JSON backup, or **Restore settings… / সেটিংস ফিরিয়ে আনুন…** to replace widget preferences immediately. Backups contain language, theme, font size, transparency, position/lock, speed units, refresh interval, adapter selection and optional global shortcut. They exclude **Start with Windows**, usage history and error logs. Keep the JSON file to restore after reinstalling Windows or on another PC.
- Restore adapts positions to current monitors and selects Auto if the saved adapter is missing. Corrupt, incomplete, unsupported or oversized backups are rejected; failed saves and global shortcut conflicts keep current settings intact. If Ctrl+Alt+T conflicts, free it or disable it before making the backup. Hidden restores keep the widget hidden and preserve restored settings on exit.
- Choose **Reset position / অবস্থান ফিরিয়ে আনুন** from the widget or tray to return to the primary monitor's bottom-right work area. This preserves the lock, visibility and other settings. Failed saves restore the previous position.
- **Speed unit / গতির একক** selects bytes per second (binary KB/s/MB/s/GB/s, preserving the original display) or decimal bits per second (Kbps/Mbps/Gbps). **Refresh interval / গতি আপডেটের ব্যবধান** offers 0.5, 1 (default) or 2 seconds. Both are saved; speed always uses actual elapsed time. Failed saves restore the previous unit, interval and checkmarks.
- Choose **Lock position / অবস্থান লক করুন** from the widget or tray's right-click menu to prevent accidental dragging. Clear the checkmark to move it again. Both menus stay synchronized, including while the widget is hidden. Locking saves the current position immediately; the lock survives restart. Existing settings default to unlocked, and a failed save leaves the previous state and saved position intact.
- Position lock affects manual dragging only. Monitor removal, resolution/DPI/work-area changes and enlarging the widget can still move it into a visible work area.
- Choose **Text size / লেখার আকার** or **Background transparency / পটভূমির স্বচ্ছতা** from the widget's right-click menu. Text sizes are 12, 14 (default), 16, 18, 20 and 24. Transparency offers Default, 0%, 25%, 50%, 75% and 90%; it changes the background only, keeping text, arrows and menus fully visible. Default restores the previous background appearance.
- Appearance changes apply immediately and persist across restart and light/dark theme changes. Enlarging the widget keeps it within the monitor work area. Failed saves restore the previous style/checkmarks and preserve the settings file. Older or unsupported saved appearance values use the original defaults.
- Right-click the widget to change its theme or enable **Start with Windows** (at sign-in).
- Settings are stored in `%APPDATA%\TrafficSense\settings.json`. Corrupt settings fall back to defaults; failed saves show an error and preserve the previous file.
- Startup registration is updated only when you change **Start with Windows**. Its checkbox reflects the registration for the current executable, independently of the settings file. After moving the app, enable it again from its new location.
- The widget returns to a visible monitor work area when a monitor is removed, the resolution/DPI changes, or the taskbar work area changes. Existing saved window positions are migrated automatically.

The Windows lifecycle and portable probes exercise appearance changes, screen-edge resizing, bilingual menus, locked-file rollback, hidden monitoring and restart with temporary settings. Run the appearance checks alone with `dotnet run --project tests/TrafficSense.LifecycleTests -c Release -- --appearance`. Resource stress checks also cycle text size and transparency.

Position-lock checks also cover widget/tray clicks, the drag guard, immediate position saving, failed-save coordinate rollback, hidden monitoring, display/work-area notifications, edge resizing and second-process locked/unlocked restart. Run them alone with `dotnet run --project tests/TrafficSense.LifecycleTests -c Release -- --position-lock`; resource stress also toggles position lock and disposes temporary tray menus.

Backup validation tests cover complete round trips, corrupt/missing/duplicate/unknown properties, unsupported versions, invalid preference values, size limits and atomic-write failures. Run the Windows restore checks alone with `dotnet run --project tests/TrafficSense.LifecycleTests -c Release -- --backup`; they use temporary files and fake shortcut registration to verify live/hidden restore, language/tray/timer/appearance updates, missing adapter/monitor recovery, failure rollback and hidden-exit persistence.

## System Tray & Single Instance

- Only one instance runs per Windows user in the current sign-in session, even when launched from different folders. Starting it again shows the existing widget, including when it was hidden.
- The system tray icon uses green download and blue upload arrows. Right-click it to toggle **Show widget** or choose **Exit**. Double-click the icon to show the widget.
- The widget's own right-click menu also has **Hide widget**. Hiding keeps monitoring active and preserves the current position; use Exit to save settings and close the app.
- Hidden monitoring continues reading counters and calculating speeds, while text/layout rendering waits until the widget is shown again. Showing it renders the latest sample without resetting the monitoring baseline.
- After a normal exit or crash, another launch can take ownership. Older versions without single-instance support should be closed before running this version.

## Error Logs & Recovery

- Choose **About TrafficSense** from the widget or tray menu to see the running app's version and copy the error log folder address. **Open log folder** creates the folder if needed and opens it in File Explorer. Opening About keeps a hidden widget hidden; repeated clicks reuse the same window, which closes when the app exits. About follows English/Bengali language changes immediately.

- Error details are stored locally in `%LOCALAPPDATA%\TrafficSense\logs\errors.jsonl`. Records include UTC time, process ID, app/runtime version, error category and exception details. Logs are not uploaded automatically; review them before sharing, since Windows diagnostics can include local paths.
- The logger rotates two older files (`errors.1.jsonl`, `errors.2.jsonl`), keeping three files of up to 256 KiB each. Individual messages are truncated to 16,000 characters. Logging is best effort: a locked/full/unwritable location or a busy logger during termination must not cause another error.
- Unexpected UI or startup failures show one short English/Bangla message with the saved log path, or explain that the log could not be saved. Monitoring and tray controls stop before the fatal UI message; the app then exits with an error status. Start it again to resume monitoring. Fatal shutdown retains the last saved settings rather than writing potentially invalid in-memory state.
- An unhandled worker-thread exception is logged synchronously without waiting for the UI or showing a background dialog; the .NET runtime terminates the process. Unobserved task exceptions are recorded without a popup; asynchronous operations should still be awaited and handle their own expected failures.
- Recoverable settings/startup errors keep the existing rollback behavior and are logged. Corrupt/unreadable settings are recorded and load defaults. An ordinary disconnected adapter is not treated as a crash and does not generate repeated error dialogs.

The Windows lifecycle probe runs isolated startup/UI/close failures, an actual terminating worker, blocked-log and failed-reporting cases, plus recoverable settings/task reporting. It uses temporary logs/settings and never crashes the user's running widget. Run these checks alone with:

```powershell
dotnet run --project tests/TrafficSense.LifecycleTests -c Release -- --error-handling
```

Unit checks cover valid diagnostic records, concurrent writers, bounded rotation, Unicode/oversized details, locked paths and corrupt-settings fallback. The normal lifecycle and portable verification run the error probes automatically. Hard process kills, power loss, stack overflow and other failures that bypass managed handlers may leave no log.

## Usage, graph and keyboard controls

Open **Usage and history / ডেটা ব্যবহার ও ইতিহাস** from the widget or tray (or Ctrl+H while the widget is focused). It shows today's and this month's measured upload/download, plus the last two minutes of speed. The graph uses solid download and dashed upload lines, a text summary, and the selected speed unit. It stays independent of widget visibility; repeated opening reuses the same window and exiting closes it.

Choose **CSV period / CSV সময়সীমা** in that window: **All retained days** (default, up to 400 recorded days), **Month** (current/retained months), or **Date range**. Enter custom boundaries as `YYYY-MM-DD`; both days are included. The selected recorded-day count and **Selected period usage / নির্বাচিত সময়ের ডেটা ব্যবহার** update live with download, upload and combined totals. These summaries use rounded B/KB/MB/GB amounts (1024 bytes per KB); CSV keeps exact bytes. Totals cover the same retained records as the export, including the latest measured traffic. Empty periods show zero with an explanation; invalid dates hide totals until corrected. Missing/invalid/reversed dates or no matching records disable export. Choices remain intact during live refresh, language changes and month rollover; reopening the window defaults to all retained days. On smaller screens, scroll to reach the controls.

The **Daily usage chart / দিনভিত্তিক চার্ট** beneath those totals shows up to 30 calendar days ending at the latest recorded day in the selected period, oldest to newest. Filled green bars show download and outlined blue bars show upload on a shared byte scale; the top label is the largest download or upload amount. Missing records leave gaps; recorded zero values have a baseline mark. In high contrast, system colors and filled/outlined bars distinguish the two series. The English/Bengali description and exact-byte table provide text alternatives. The chart updates live without animation; the summary and table still cover the entire selected period.

Hover anywhere inside a day's column in the chart's plot area to see its ISO date and exact download, upload and combined bytes, even above a tiny bar. A gap shows that no stored record exists for that date; a recorded zero shows zero bytes. Details follow the current language, new samples, filter changes and chart resizing without requiring another mouse move. Leaving the chart dismisses the popup. Invalid/empty selections and closing history clear it. Native tooltips use system colors and wrapping; keyboard/screen-reader users can read the same exact values in the table.

The read-only **Daily usage / দিনভিত্তিক হিসাব** table below the chart shows the same selected records as CSV, newest first, with ISO dates and rounded download/upload/combined amounts. Hover a cell for exact byte counts; keyboard arrow keys navigate rows. The list updates live while retaining existing rows, selection and scrolling. Empty or invalid periods clear and hide the chart and list; the explanation above remains visible. At most 400 retained days are listed, with a scrollable, virtualized table on smaller screens.

Choose **Export CSV… / CSV সংরক্ষণ করুন…** to save those records, including the most recent sampled traffic. The file has stable English columns `Date,DownloadBytes,UploadBytes,TotalBytes`, ascending `yyyy-MM-dd` local dates, and exact integer byte counts regardless of UI language or speed units. It uses comma separators, UTF-8 with BOM and CRLF lines for spreadsheet import. Convert bytes to MiB by dividing by 1,048,576. Days without retained records are omitted. Cancel and failed writes preserve the previous file and local usage; exporting does not clear pending persistence or reset totals.

CSV regression tests check regional formats, exact large totals, retained days, inclusive ranges, leap/year boundaries, strict ISO date validation, unsaved samples and atomic replacement failures. Chart tests check inclusive 30-calendar-day bounds, chronological ordering, missing/zero records, immutable snapshots and maximum counters. Windows UI checks also verify the bilingual chart and its accessibility peer, filter/live/rollover agreement with daily rows and CSV, unchanged snapshot reuse, exact-byte details, selection/scroll retention, 400-row virtualization, empty/invalid clearing, cancel, traffic arriving during destination selection, localized errors, minimum-width/scroll layout and hidden-widget visibility. Resource stress also cycles filter modes and invalid date entry. Run the UI checks alone with `dotnet run --project tests/TrafficSense.LifecycleTests -c Release -- --csv`.

Totals cover valid sampled intervals while TrafficSense runs, across whichever single connection is selected. Initial/reconnected/reset counters and sleep/stalled/app-off gaps do not count; this avoids counting lifetime counters or VPN/underlying traffic twice. An interval crossing midnight is assigned to its ending local date. Clock/date changes follow the current local date. These are measured totals, not ISP billing figures. The latest 400 days are stored atomically in `%LOCALAPPDATA%\TrafficSense\usage.json`, every minute and on normal exit. A crash/hard shutdown can lose the last unsaved minute. Failed writes retain pending data for retry and log the error without repeated dialogs. Corrupt/unreadable files fall back to an empty ledger.

With the widget focused: **Ctrl+L** lock/unlock, **Ctrl+R** reset position, **Ctrl+H** usage/history, **F1** About, **Escape** hide, **Shift+F10** or the Menu key open the context menu, and **arrow keys** move it by 10 logical pixels (**Shift+arrow** by 1). Position lock blocks keyboard movement too. Menus support standard keyboard navigation. The optional **Global shortcut (Ctrl+Alt+T)** setting shows/hides the widget from any app; it is off by default, is saved when enabled and reports a conflict if the key is unavailable. Registration is released on exit. If a saved shortcut cannot register at startup, its checkbox stays off and the log records the failure; turn it off/on to retry.

Localized screen-reader names/help identify speeds and keyboard commands. History uses Windows system colors, and graph line styles/text complement colors. These improvements are not a claim of a complete accessibility audit. Run isolated checks with `dotnet run --project tests/TrafficSense.LifecycleTests -c Release -- --features`.

### Build and verify Setup

After `Build-Portable.ps1`, use its `app` directory:

```powershell
./scripts/Build-Installer.ps1 -SourceDirectory '.artifacts/portable/<build-id>/app' -VerifyInstall
```

This uses the pinned, SHA-256-verified official Inno Setup 7.1.0 compiler in portable mode under `.artifacts/tools`, or an explicit `-Compiler` path. Verification compiles a separate test identity, installs only into a checked temporary workspace directory, verifies payload hashes and repair/upgrade, tests the running-app guard and startup-entry ownership, uninstalls, and checks that user-created files survive. It never launches the installed widget or modifies the real TrafficSense startup value. CI runs these checks before publishing installer assets.

## Manual update checks

Choose **About TrafficSense → Check for updates**. The app contacts GitHub only when you click, compares the latest published stable release with the running version, and offers **View release and download** when a newer version is available. The button opens this project's release page in your default browser; it does not download or install software automatically. A copyable page address remains available if the browser cannot open.

Checks run without blocking the widget, allow only one request at a time, time out after ten seconds, and cancel when About closes. Offline/server errors and GitHub request limits show a localized status with a retry button. Opening About or starting TrafficSense makes no update request. The request uses [GitHub's public release API](https://docs.github.com/en/rest/releases/releases#get-the-latest-release) without an authentication token or sending settings/logs. These checks are separate from network speed monitoring.

The Windows lifecycle/portable probe includes deterministic update scenarios without contacting GitHub or opening a browser. Run them alone with `dotnet run --project tests/TrafficSense.LifecycleTests -c Release -- --updates`. For an optional real request to GitHub, use `--update-live`; this is excluded from automated CI to avoid network-dependent failures.

## Language / ভাষা

- Right-click the widget or tray icon → **Language / ভাষা** → **English** or **বাংলা**. Both language names remain visible in either UI language.
- Changes apply immediately to widget/tray menus, tooltips, adapter status, and app error summaries, including while the widget is hidden. Monitoring, adapter selection, and widget position are preserved.
- The language is remembered in the settings file (`en` or `bn`). Existing settings without a language, or unsupported values, default to English. A failed save keeps the previous language and menu checkmarks.
- Adapter names/descriptions, Windows diagnostic details, and standard speed units (`B/s`, `KB/s`, `MB/s`, `GB/s`) retain their original form. Numeric formatting continues to follow the existing Windows culture.
- বাংলা বেছে নিতে widget বা tray icon-এ ডান-ক্লিক করে **Language → বাংলা** নির্বাচন করুন। আবার ইংরেজিতে ফিরতে **ভাষা → English** নির্বাচন করুন।

## Network Adapter & VPN Traffic

- Right-click the widget → **Network adapter** to select **Auto (one connection)** or an adapter by name. Hover over the widget to see which connection is being monitored.
- Auto monitors **one adapter at a time**, following the Windows route to a representative Internet IPv4 destination when available. Otherwise (including IPv6-only connections), it prefers a gateway-backed connection and then Ethernet/Wi-Fi, with lower priority for local Hyper-V/WSL/VM adapters. Equally preferred connections retain the current selection to avoid switching every second.
- VPN traffic and the underlying Ethernet/Wi-Fi traffic are never summed. To measure only the VPN tunnel, select its adapter explicitly. An underlying connection also includes VPN overhead and traffic outside the tunnel; their readings can differ.
- Manual selection is remembered across restarts. If that adapter disconnects or disappears, the widget shows zero and waits for it to reconnect. Choose Auto to allow automatic failover.
- Connecting, disconnecting, or switching adapters starts a fresh speed baseline. Auto does not report a combined total across simultaneous connections. With split tunneling or destination-specific routes, select the adapter whose traffic you want to measure.
- Sleep/resume clears the previous speed and counter baseline. Address/availability notifications also reset the baseline at the next sample, so a quick reconnect between timer ticks does not count traffic across the interruption. The first sample establishes the new baseline at zero; the following one-second interval reports current speed.
- A sampling gap longer than five seconds also starts a fresh interval, covering missed power notifications and long UI stalls. Shorter delayed ticks still use their actual elapsed time. These transitions preserve the selected adapter, language, position, and hidden state.

The lifecycle probe includes an isolated recovery check using controlled adapter counters and timestamps. It sends suspend/resume messages only to its test window and calls the production network notification handler from a worker thread; it never sleeps the computer or disconnects a real adapter. It covers manual reconnect, Auto VPN route changes, hidden recovery, repeated notifications and callbacks after Exit. Run it alone with:

```powershell
dotnet run --project tests/TrafficSense.LifecycleTests -c Release -- --network-recovery
```

The normal lifecycle and portable checks run it automatically. These simulations verify application behavior; physical sleep, Modern Standby and driver-specific reconnect behavior still need a device test.

## 🤝 Contributing

### Prepare a release

The application version is set in `src/TrafficSense.csproj`; the same value is used by About, file metadata and diagnostic records. The SDK adds the source commit to the informational version when available.

Before publishing, add or update the matching section in [CHANGELOG.md](CHANGELOG.md), then run:

```powershell
./scripts/Prepare-Release.ps1 -Tag v1.5.0
```

This validates that the proposed tag matches the app version and extracts the corresponding notes into `.artifacts/release-notes.md`. It does not create a tag or GitHub release. CI validates the notes on ordinary builds. The portable ZIP includes the changelog and generated `RELEASE-NOTES.md`; packaging checks both the executable metadata and the bundled app's version against the project version.

After approval to publish, replace the changelog's pending status with the release date, update its comparison links and the download status above, merge the release preparation and tag that tested commit. Pushing a `v*` tag runs the release workflow, which rejects a mismatched app version, verifies the portable package and publishes its ZIP/checksum with the extracted changelog notes.

Contributions are always welcome! Feel free to open an issue or submit a pull request.

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---
**Made with ❤️ for the community!**
