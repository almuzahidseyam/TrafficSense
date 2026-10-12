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
3. Extract and run `TrafficSense.exe`. No installation required!

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
