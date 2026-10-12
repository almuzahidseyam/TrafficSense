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

Version **1.3.0 is being prepared**. See [CHANGELOG.md](CHANGELOG.md) for the changes and upgrade instructions; it is not yet a published release.

1. Go to the [Releases](https://github.com/almuzahidseyam/TrafficSense/releases) page.
2. Download the latest `TrafficSense.zip`.
3. Extract the entire ZIP and run `TrafficSense.exe` on **64-bit Windows 10/11**. The prepared 1.3.0 portable package includes .NET 8; older releases may require a separate .NET 8 Desktop Runtime installation.

Exit an older running version from its tray menu before opening the new EXE. Hiding it keeps it running, and a second launch will show that older instance. Settings remain in `%APPDATA%`, and the bundled native runtime is extracted to the Windows temporary folder on first launch.

Releases also include `TrafficSense.zip.sha256`. To check a download, compare the result of `Get-FileHash ./TrafficSense.zip -Algorithm SHA256` with that file.

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

CI uploads the verified ZIP and checksum as `TrafficSense-win-x64-portable`. The release workflow runs the same checks and attaches both files when a `v*` tag is pushed. Ordinary builds and pull requests do not publish a release. Rebuild packages with an updated SDK/runtime for .NET security servicing; the bundled runtime does not automatically use an installed runtime update.

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

- Right-click the widget to change its theme or enable **Start with Windows** (at sign-in).
- Settings are stored in `%APPDATA%\TrafficSense\settings.json`. Corrupt settings fall back to defaults; failed saves show an error and preserve the previous file.
- Startup registration is updated only when you change **Start with Windows**. Its checkbox reflects the registration for the current executable, independently of the settings file. After moving the app, enable it again from its new location.
- The widget returns to a visible monitor work area when a monitor is removed, the resolution/DPI changes, or the taskbar work area changes. Existing saved window positions are migrated automatically.

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
./scripts/Prepare-Release.ps1 -Tag v1.3.0
```

This validates that the proposed tag matches the app version and extracts the corresponding notes into `.artifacts/release-notes.md`. It does not create a tag or GitHub release. CI validates the notes on ordinary builds. The portable ZIP includes the changelog and generated `RELEASE-NOTES.md`; packaging checks both the executable metadata and the bundled app's version against the project version.

After approval to publish, replace the changelog's pending status with the release date, update its comparison links and the download status above, merge the release preparation and tag that tested commit. Pushing a `v*` tag runs the release workflow, which rejects a mismatched app version, verifies the portable package and publishes its ZIP/checksum with the extracted changelog notes.

Contributions are always welcome! Feel free to open an issue or submit a pull request.

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---
**Made with ❤️ for the community!**
