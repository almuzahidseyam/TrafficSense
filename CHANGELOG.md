# Changelog

Changes are grouped by release. Version 1.3.0 is prepared but has not been published.

## [Unreleased]

## [1.3.0] - Pending release

### Added

- English and Bengali menus, tooltips and app messages, with a saved language choice and immediate switching.
- System tray controls to show/hide the widget, choose a language and exit. A second launch shows the existing widget instead of creating a duplicate.
- Network adapter selection with automatic or manual monitoring of one connection.
- About window with the running app's version, a copyable error log folder address and an **Open log folder** button. It is available from the widget and tray even while the widget is hidden.
- Local error logs with bounded rotation, localized error summaries and safe shutdown after unexpected UI/startup failures.
- A self-contained Windows x64 portable ZIP, including .NET 8, license notices and a SHA-256 checksum. A separate .NET installation is no longer required for this package.

### Fixed

- VPN and underlying connection traffic are no longer added together. Automatic selection monitors one adapter at a time; manual selection waits for the chosen adapter to reconnect.
- Speed calculations use actual elapsed time, handle counter resets and establish a fresh baseline after sleep/resume, reconnects or sampling gaps longer than five seconds.
- The widget returns to a visible monitor work area after monitor, resolution, DPI or taskbar changes.
- Start with Windows reflects the registration for the current executable and reports failed changes. Failed settings saves preserve the previous file and selection.
- Showing the widget restores the latest sampled speed. Hidden widgets keep monitoring without unnecessary text/layout updates; rapid show/hide cycles no longer accumulate transparent-window resources.
- Unexpected UI failures stop monitoring and close the app while retaining the last successfully saved settings. Logging failures do not trigger another logger error.
- App version metadata now identifies 1.3.0, replacing the SDK's implicit 1.0.0 default. About and diagnostic records also include the build commit when available.

### Verification

- Automated checks cover network calculations, settings/startup behavior, localization, single-instance ownership and diagnostic logging.
- Isolated Windows probes exercise real widget/tray interactions, reconnect/resume behavior, About, recoverable errors and fatal shutdown.
- Portable verification checks the extracted ZIP and bundled runtime. CI also measures visible/hidden monitoring resources with a finite stability run; this does not guarantee behavior over days or every driver/device combination.

### Upgrading from 1.2.0

- Exit the running version from its tray menu before launching the new executable. If the older version has no tray menu, use its widget's Exit command. Starting another copy may only restore the existing instance.
- Extract the entire new ZIP into its intended folder. Settings remain in `%APPDATA%\TrafficSense\settings.json`; logs remain in `%LOCALAPPDATA%\TrafficSense\logs`.
- After moving the executable, enable **Start with Windows** again from the new location if you use that setting.
- Supports 64-bit Windows 10/11. The larger ZIP includes the .NET runtime and can run without a separate .NET installation.

## [1.2.0] - 2026-09-14

- Previous published release with a Windows ZIP. Its original GitHub release has no written release notes; this changelog does not reconstruct undocumented changes in older releases.

[Unreleased]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.2.0...HEAD
[1.3.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/almuzahidseyam/TrafficSense/releases/tag/v1.2.0
