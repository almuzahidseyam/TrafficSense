# Changelog

Published releases carry their publication date. Intermediate development milestones below 1.11.0 were bundled into 1.11.0 rather than published separately.

## [Unreleased]

## [1.12.0] - 2026-10-12

### Added

- English/Bengali **Settings backup** menu on the widget: export a JSON backup and restore language, theme, font size, background transparency, position/lock, speed units, refresh interval, network adapter and optional global shortcut without restarting.
- Versioned, size-bounded backup validation rejects malformed, incomplete, unsupported or invalid files before changing preferences. Exports replace files atomically. Save failures and shortcut conflicts keep current preferences and shortcut registration intact.
- Restore fits positions to current monitors and uses Auto when a saved adapter ID is missing. Hidden restores preserve visibility, defer appearance/position rendering until shown, and retain restored coordinates if the app exits while hidden.

### Upgrading from 1.11.0

- Exit the old widget, then run Setup or extract the new portable ZIP. Existing settings and usage remain compatible.
- Right-click the widget → **Settings backup → Back up settings… / Restore settings…**. Restore replaces widget preferences immediately. **Start with Windows**, usage history and error logs are excluded from the backup. A global shortcut conflict cancels the entire restore; free Ctrl+Alt+T or disable it before creating the backup.

## [1.11.0] - 2026-10-12

### Added

- Saved position lock/unlock and one-click position reset from widget/tray menus.
- Saved byte/bit speed units and 0.5/1/2-second refresh intervals.
- Daily/monthly measured upload/download totals with bounded, atomic local persistence, and a two-minute speed graph in Usage and history.
- Keyboard controls, optional Ctrl+Alt+T global show/hide shortcut, screen-reader names/help and graph line/text alternatives to color.
- Per-user Windows installer/uninstaller alongside the self-contained portable ZIP. In-use upgrades are blocked, uninstall removes only startup entries pointing to that installed copy, and settings/usage/logs are preserved.

### Upgrading from 1.5.0

- Exit the old widget from its tray menu. Run `TrafficSense-Setup.exe`, or extract `TrafficSense.zip` and run `TrafficSense.exe`. Both include .NET 8 and support Windows 10/11 x64. Settings remain compatible.
- Usage totals cover valid intervals measured while the app runs, across selected connections; sleep/reconnect/app-off gaps are excluded. Totals use local dates and are not ISP billing figures. Normally up to one minute of unsaved usage can be lost on a crash or hard shutdown; failed writes can leave more pending data.
- Right-click the widget or tray for position controls and Usage and history. Widget menus also offer units, refresh interval and an optional global shortcut (off by default). With the widget focused: Ctrl+L lock, Ctrl+R reset, Ctrl+H history, F1 About, Escape hide, Shift+F10 menu, arrows move (Shift for fine movement).

## [1.10.0] - Development milestone

### Added

- Keyboard commands for locking, resetting, history, About, hiding, context menu and moving the focused widget. Optional saved Ctrl+Alt+T global show/hide shortcut detects conflicts and releases registration on exit/fatal shutdown. Screen-reader names/help, standard system colors in history, and text/dashed-line alternatives to graph colors improve accessibility.

## [1.9.0] - Development milestone

### Added

- Bounded, in-memory two-minute download/upload graph in Usage and history. It follows the selected speed unit, uses distinct solid/dashed lines and a textual current-speed summary, and leaves gaps across suspended sampling. Hidden widget monitoring continues; history is not collected outside the app's runtime.

## [1.8.0] - Development milestone

### Added

- Local daily/monthly upload/download totals for valid measured intervals on the selected connection. Bounded history (400 days) is atomically saved every minute and on normal exit in `%LOCALAPPDATA%/TrafficSense/usage.json`. Sleep/reconnect/app-off gaps and initial counter baselines do not inflate totals. Usage/history opens from the widget or tray and follows the UI language.

## [1.7.0] - Development milestone

### Added

- Saved byte/bit speed units (legacy binary KB/s–GB/s or decimal Kbps–Gbps) and 0.5/1/2-second refresh intervals. Changes preserve the measurement baseline; actual elapsed time determines speed. Failed saves restore the menu/timer state.

## [1.6.0] - Development milestone

### Added

- Saved **Lock position** setting in the widget and tray menus, with English/Bengali labels and synchronized checkmarks. Locking saves the current position and prevents manual dragging; unlocking restores dragging. Monitor/work-area recovery and appearance resizing still keep the widget visible. Failed saves restore the previous state and coordinates. Existing settings remain unlocked.
- One-click **Reset position** from widget/tray menus returns to the primary monitor's bottom-right work area, keeps the lock/visibility and other preferences, and rolls back on save failure.

## [1.5.0] - 2026-10-12

### Added

- Saved widget text-size and background-transparency choices in the widget's English/Bengali context menu. Changes apply immediately, resize/clamp the widget to its monitor work area and preserve readable text/arrows. Failed saves keep the previous appearance and selections; older settings retain the original appearance.

### Fixed

- Rapid appearance changes while the transparent widget is hidden no longer retain native drawing resources. Appearance updates are combined into the next visible frame; hidden monitoring continues normally.

### Upgrading from 1.4.0

- Exit the current widget from its tray menu, extract the new ZIP and run `TrafficSense.exe`. Existing settings are preserved. The Windows x64 portable package includes .NET 8.
- Right-click the widget to choose **Text size** or **Background transparency**. Text size 14 and Default transparency retain the original appearance; text and arrows keep full opacity.

## [1.4.0] - 2026-10-12

### Added

- Manual update checks in About, with English/Bengali status messages and a release page/download button when a newer stable version is available. Checks handle offline connections, timeouts and GitHub rate limits without blocking the widget, and are cancelled when About closes. Release links stay within this project's GitHub repository; nothing is installed automatically.

### Upgrading from 1.3.0

- Exit the current widget from its tray menu, extract the new ZIP and run `TrafficSense.exe`. Existing settings are preserved. The Windows x64 portable package includes .NET 8.
- Open **About TrafficSense → Check for updates** to check future releases. If a newer version is available, **View release and download** opens its GitHub page in your browser.

## [1.3.0] - 2026-10-12

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

[Unreleased]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.12.0...HEAD
[1.12.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.11.0...v1.12.0
[1.11.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.5.0...v1.11.0
[1.5.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/almuzahidseyam/TrafficSense/releases/tag/v1.2.0
