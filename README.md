# TrafficSense 🚀 

**A Modern, Lightweight, and Premium Network Traffic Monitor for Windows.**

TrafficSense is a beautiful and accurate real-time network speed monitor for Windows. Built with C# and WPF, it features a modern UI with Fluent Design (Mica/Acrylic effects), completely tailored for a premium user experience. It perfectly supports both English and Bengali (বাংলা) languages.

> **Note:** A screenshot of the app will be added here soon!

## 🌟 Features

- **Real-Time Speed:** Accurate upload and download speed tracking.
- **Premium UI:** Clean, modern, and lightweight design using Windows 11 Fluent design principles (Mica/Acrylic).
- **Language Support:** Built-in support for English and Bengali (বাংলা).
- **Lightweight:** Uses minimal RAM and CPU.
- **Easy Installation:** Simple `.exe` installer (No complicated setups!).
- **Customizable:** Change colors, themes (Light/Dark), and widget position.
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

## Settings & Window Position

- Right-click the widget to change its theme or enable **Start with Windows** (at sign-in).
- Settings are stored in `%APPDATA%\TrafficSense\settings.json`. Corrupt settings fall back to defaults; failed saves show an error and preserve the previous file.
- Startup registration is updated only when you change **Start with Windows**. Its checkbox reflects the registration for the current executable, independently of the settings file. After moving the app, enable it again from its new location.
- The widget returns to a visible monitor work area when a monitor is removed, the resolution/DPI changes, or the taskbar work area changes. Existing saved window positions are migrated automatically.

## 🤝 Contributing

Contributions are always welcome! Feel free to open an issue or submit a pull request.

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---
**Made with ❤️ for the community!**
