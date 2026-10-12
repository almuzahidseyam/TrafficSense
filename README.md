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

1. Go to the [Releases](https://github.com/almuzahidseyam/TrafficSense/releases) page.
2. Download the latest `TrafficSense.zip`.
3. Extract the entire ZIP and run `TrafficSense.exe` on **64-bit Windows 10/11**. The portable package includes .NET 8; no separate .NET installation is required.

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

## Settings & Window Position

- Right-click the widget to change its theme or enable **Start with Windows** (at sign-in).
- Settings are stored in `%APPDATA%\TrafficSense\settings.json`. Corrupt settings fall back to defaults; failed saves show an error and preserve the previous file.
- Startup registration is updated only when you change **Start with Windows**. Its checkbox reflects the registration for the current executable, independently of the settings file. After moving the app, enable it again from its new location.
- The widget returns to a visible monitor work area when a monitor is removed, the resolution/DPI changes, or the taskbar work area changes. Existing saved window positions are migrated automatically.

## System Tray & Single Instance

- Only one instance runs per Windows user in the current sign-in session, even when launched from different folders. Starting it again shows the existing widget, including when it was hidden.
- The system tray icon uses green download and blue upload arrows. Right-click it to toggle **Show widget** or choose **Exit**. Double-click the icon to show the widget.
- The widget's own right-click menu also has **Hide widget**. Hiding keeps monitoring active and preserves the current position; use Exit to save settings and close the app.
- After a normal exit or crash, another launch can take ownership. Older versions without single-instance support should be closed before running this version.

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

## 🤝 Contributing

Contributions are always welcome! Feel free to open an issue or submit a pull request.

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---
**Made with ❤️ for the community!**
