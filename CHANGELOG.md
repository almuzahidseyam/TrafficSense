# Changelog

Published releases carry their publication date. Intermediate development milestones below 1.11.0 were bundled into 1.11.0 rather than published separately.

## [Unreleased]

## [1.26.0] - 2026-10-13

### Changed

- Realtime graphs leave gaps for baseline, disconnected, delayed and invalid samples instead of presenting them as measured idle traffic. The current history legend shows unavailable when its last sample is unknown; valid zero remains a plotted zero. Invalid values cannot inflate the graph scale.
- Added product-first English/Bengali getting-started instructions, actual WPF screenshots with clearly illustrative data, an upstream compatibility matrix, measurement methodology, reproducible accuracy traces, contribution guidance and structured issue forms.

### Validation

- Added 4,096-sample jitter traces at every supported interval, exact byte conservation beyond 4 GiB, and single-byte deltas above double's exact lifetime-integer range. These verify arithmetic and timing behavior, not comparative accuracy across physical network drivers.
- Native WPF graph tests verify separate line figures across unavailable samples. Portable, installer and resource-stability checks remain part of CI.
- Stalled worker deliveries add an unavailable history point even when their old timestamp is unchanged. The optional-metrics UI probe waits for published data rather than racing the beginning of a provider read.

## [1.25.0] - 2026-10-13

### Added

- Native font selection, separate background/download/upload/system text colors, Classic/Ocean/Monochrome presets and bounded appearance-only JSON skin import/export. Changes preserve network/position/history preferences, save atomically and roll back on failure. Windows high-contrast colors override custom colors for readability. Panel styling follows the selected font/colors.
- Optional daily/monthly combined download + upload alerts with explicit GiB limits. Only a newly observed threshold crossing notifies; startup totals and already-exceeded reconfigured limits remain quiet. Local calendar periods rearm independently and exact decimal totals avoid overflow. Windows can suppress tray balloons through its notification settings.
- A data-only extension protocol for up to eight named sensors, with bounded JSON reads, finite numeric validation and five-second publication freshness. Unavailable/old/invalid sources never turn into fabricated zero readings. No native plugin DLL is loaded into the network monitor.
- A separately instanced **Sensor bridge** mode connects to an already running local LibreHardwareMonitor web server, reads numeric Celsius temperatures and publishes the extension file atomically. Requests are loopback-only, proxy/redirect disabled, response size/time bounded and disconnect/close cancel polling. The bridge installs no hardware drivers. Provider hardware support, permissions and actual sensor update freshness remain the provider's responsibility; older providers without numeric RawValue are unsupported.
- Visible sampling/unavailable/delayed status in the floating widget. System rate counters rebaseline after provider/sleep gaps instead of presenting long-interval averages as current utilization.
- Regressions for skins, original backup compatibility, exact limits/calendar rollover, malformed/stale/oversized sensor files, culture-independent bridge parsing and isolated UI/cancellation. Probe notifications use an injected sink and do not display user alerts.

### Upgrading from 1.24.0

- Exit the widget and any sensor bridge window before upgrading. New alerts/sensors remain off by default; existing settings/history/backup files remain compatible.
- The optional bridge needs LibreHardwareMonitor running with a locally accessible web server. It does not install or start that third-party app. See docs/EXTENSIONS.md.

## [1.24.0] - 2026-10-13

### Added

- Optional **Taskbar-edge panel** using the documented Windows appbar API. It reserves a strip next to the existing taskbar and displays the same network rates/selected system metrics, with History, Settings and Disable controls. The floating widget can be hidden independently. This is a separate desktop panel, not a child injected into Explorer's taskbar buttons.
- Top/bottom edge and monitor selection, with the widget's monitor as default and primary-monitor fallback for disconnected named monitors. Position follows shell work-area/display/DPI notifications; Explorer's TaskbarCreated broadcast registers the appbar again. Full-screen and window-arrangement notifications adjust panel visibility/z-order.
- Immediate saved preferences with rollback, registration-failure fallback to the floating widget, compatible older backups, and reservation/hook cleanup on Disable and Exit.
- Isolated native WPF probes exercise a simulated shell API boundary for top/bottom positioning, independent visibility, bilingual controls, Explorer/DPI/fullscreen callbacks, save/registration failure, repeated cycles and cleanup. These tests do not reserve or rearrange the user's real desktop; direct Windows 10/11 shell compatibility still needs manual device coverage.

### Upgrading from 1.23.0

- Exit the old widget before upgrading. The panel defaults to off. Enable it from the widget's right-click menu; use the tray to hide/show the floating widget separately.

## [1.23.0] - 2026-10-13

### Added

- Optional CPU utilization, physical RAM usage, Windows-reported CPU frequency, busiest GPU engine utilization and disk utilization/read/write rates. Select individual items in **Display system metrics**. English PDH paths support localized Windows; absent/invalid/stale counters show Unavailable rather than zero. Monitoring uses background reads once per second, with no added hardware drivers or elevation.
- Reusable **System and connection details** window with all optional counters, physical memory totals, IP addresses, link capacity, interface byte counters and measurement explanations. Closing the window returns to only the selected widget counters; disabling all items stops optional polling and releases native queries.
- Process contributions are combined only for the same physical GPU engine. Distinct engines/GPUs are not summed into a misleading utilization. Disk activity is derived from total idle time; frequency may be nominal, as supplied by Windows. Temperature is not inferred from unrelated sensors.
- Bilingual controls, immediate settings save with rollback, hidden-render guard, compatible import of older settings backups and native/pure regression coverage.

### Upgrading from 1.22.0

- Exit the old widget before upgrading. Optional system metrics default to off. Existing settings, history and older complete backup files remain supported.

## [1.22.0] - 2026-10-13

### Added

- Select plain text or CSV when copying a daily record. CSV shares the export schema, exact integers, ISO date and CRLF line breaks in both languages; changing format clears stale confirmation.
- Serialized background counter sampling independent of the WPF dispatcher, a 250 ms refresh option, counter-read timestamps, IPv6 route fallback, IP/link details and explicit sample-quality descriptions in the widget tooltip.
- Exact byte buffering by local sample day, once-only delivery, bounded retention and exit draining. Configuration/reconnect/exit invalidate in-flight reads; errors reset stale counters and diagnostics are rate limited.
- Counter timing, duplicate/invalid data, blocked-read concurrency, midnight accounting, failure/recovery and clipboard-format regression coverage; published measurement contract and comparison roadmap.

### Upgrading from 1.21.0

- Exit the old widget before installing or extracting. Settings and usage remain compatible. Copy format is per history window and defaults to plain text.
- The displayed rate remains a Windows interface interval average. Traffic during disconnected, suspended, closed-app or unobservable intervals cannot be reconstructed. See ROADMAP.md for the measurement contract.

## [1.21.0] - 2026-10-13

### Added

- One-click **Copy selected day's usage** button below the daily table. It copies the ISO date and exact download, upload and combined bytes in the current English/Bengali language, using Unicode text and Windows line breaks. Pending traffic is refreshed before copying; zero records and older selected days outside the chart are supported without rounding or overflow.
- Native keyboard-accessible controls and translated inline success/error feedback, exposed as a polite accessibility live region. No selected day, invalid dates and empty periods disable copying. Changing the selected record, its counters or the language clears stale feedback. Clipboard contention/unavailability is logged and allows retry without a modal dialog or lost usage.
- Isolated Windows probes verify captured clipboard text, latest unsaved samples, exact maximum totals, zeros, both languages, guards/cleanup, failures/retries, accessible feedback and minimum-size layout without reading or replacing the user's clipboard.

### Upgrading from 1.20.0

- Exit the old widget, then install Setup or extract the portable ZIP. Settings, backups and stored usage remain compatible.
- Open **Usage and history**, select a chart day or table row, then click **Copy selected day's usage**. Stored totals and CSV export scope remain unchanged.

## [1.20.0] - 2026-10-12

### Added

- Selected-day highlight in the daily usage chart: an outline and triangle drawn over the day's column using the Windows highlight color, with a translated selected-date label and accessible description. Markers remain visible on zero usage and maximum-height bars, without changing adjacent columns or axis labels.
- Selection stays synchronized in both directions: clicking the chart or selecting a daily table row updates the marker. Date-based selection survives live traffic, language changes, resizing and calendar rollover. An older selected row outside the plotted 30-day window is explained without marking a different day; filtering it back into view restores the marker. Clearing selection, invalid/empty filtering and closing history clear it.
- Isolated Windows checks cover native rendered pixels, first/last slots, narrow 30-day columns with maximum counters, zero records, bilingual descriptions, filter restoration, rollover and existing table scrolling/virtualization.

### Upgrading from 1.19.0

- Exit the old widget, then install Setup or extract the portable ZIP. Settings, backups and stored usage remain compatible.
- In **Usage and history**, select a chart day or daily table row to see the matching highlighted column and selected date. Full selected totals and CSV scope remain unchanged.

## [1.19.0] - 2026-10-12

### Added

- Click a recorded day in the daily usage chart to select the matching date in the newest-first table. The table scrolls to the row and reveals it in the outer history viewport, including with 400 retained records and small screens. The exact-byte popup closes on selection.
- A hand cursor identifies selectable days. English/Bengali help explains selection; measured zero records are selectable while gaps, right clicks and clicks outside the plot preserve the current selection. Filters, live refresh and language changes retain valid selections without changing stored usage or CSV scope.
- Isolated Windows checks cover calendar-to-row matching, filtered/zero/gap records, stale/rapid clicks, live selection retention and both levels of scrolling with virtualized rows.

### Upgrading from 1.18.1

- Exit the old widget, then install Setup or extract the portable ZIP. Settings, backups and stored usage remain compatible.
- Open **Usage and history**, then click a day's chart column to reveal its row in **Daily usage**. Keyboard users can continue navigating the daily table directly.

## [1.18.1] - 2026-10-12

### Fixed

- Stabilized retained-memory verification after popup stress. An occasional run kept roughly 91 MiB of managed heap committed despite only about 7 MiB of live objects, failing the private-memory budget while identical runs passed. The probe now reclaims unused GC regions at both measurement boundaries, retaining ordinary-collection snapshots alongside reclaimed snapshots for diagnosis. Managed/private/native budgets are unchanged.
- This is a verification correction; the widget's daily hover details behave as in 1.18.0. Production monitoring does not force garbage collections.

### Upgrading from 1.18.0

- Exit the old widget, then install Setup or extract the portable ZIP. Settings, backups and stored usage remain compatible. The daily chart's English/Bengali date and exact-byte hover details are included.

## [1.18.0] - 2026-10-12

### Added

- Hover details for the daily usage chart: anywhere in a day's plot column shows its ISO date and exact download, upload and combined bytes. Small bars are easy to inspect; missing days show a localized no-record message, while measured zeros show zero counts.
- Native English/Bengali tooltips use system colors and wrapping. Details update with language, traffic, filtering, resizing and calendar rollover while the pointer stays still. Leaving the chart dismisses the popup; invalid/empty selections and closing history clear it. The keyboard-accessible daily table remains an exact-byte alternative.
- Calendar hit testing covers slot boundaries, missing days, resizing and extreme dates. Isolated Windows probes verify real popup opening/cleanup, translations, live updates and maximum counters; resource stress also opens and disposes chart tooltips repeatedly.

### Upgrading from 1.17.0

- Exit the old widget, then install Setup or extract the portable ZIP. Settings, backups and stored usage remain compatible.
- Open **Usage and history**, scroll to **Daily usage chart**, and rest the mouse inside a day's column. The popup shows exact byte counts for the same record as the daily table and CSV. Gaps do not reconstruct missing usage.

## [1.17.0] - 2026-10-12

### Added

- Small daily usage chart between selected-period totals and the daily table. It plots up to 30 calendar days ending at the selection's latest record, with chronological dates, paired download/upload bars and a shared byte scale. Full selected totals, daily rows and CSV remain unchanged.
- Filled download and outlined upload bars distinguish series without relying on color; high contrast uses system colors. Missing records leave gaps, while recorded zero amounts have baseline marks. English/Bengali labels and an accessible image description accompany the exact-byte table.
- Filtering, language changes and new samples update the chart; unchanged records reuse its snapshot. Empty or invalid periods clear and hide it. Rendering is bounded to 30 days, has no animation/timer, and handles maximum stored counters without overflow.

### Upgrading from 1.16.0

- Exit the old widget, then install Setup or extract the portable ZIP. Settings, backups and stored usage remain compatible.
- Open **Usage and history**, choose **CSV period**, and scroll below selected-period totals to **Daily usage chart**. Its 30-day view ends at the latest record in that selection; the table below still lists every selected recorded day. Gaps indicate missing records rather than reconstructed usage.

## [1.16.0] - 2026-10-12

### Added

- Read-only daily usage table below selected-period totals, with date, download, upload and combined amounts. All retained days, a selected month and inclusive custom ranges show the same records as CSV, with the newest day first.
- English/Bengali headers and accessible exact-byte row descriptions; hovering a cell reveals exact counts even when displayed units are rounded. Invalid or empty selections clear and hide the table while keeping the existing explanation.
- Live row updates preserve selection and scrolling; language changes update existing rows. A virtualized table grows from 100 to 200 logical pixels as records are added, bounding visual work for all 400 retained days; expired records are removed on rollover. Native controls/system colors and wrapping keep the table usable at minimum width and on smaller screens.

### Upgrading from 1.15.0

- Exit the old widget, then install Setup or extract the portable ZIP. Settings, backups and stored usage remain compatible.
- Open **Usage and history**, choose **CSV period**, and scroll to **Daily usage** beneath the totals. Only recorded days are shown; CSV still exports ascending ISO dates and exact bytes. The table is for viewing and does not edit usage or clear pending persistence.

## [1.15.0] - 2026-10-12

### Added

- Live selected-period download, upload and combined usage totals below the CSV period controls, in English and Bengali. All retained days, a selected month and inclusive custom dates use the same records as CSV export.
- Valid periods with no records show zero totals and the existing explanation. Invalid/reversed dates clear and hide the summary so stale amounts cannot be mistaken for the current selection. Changing language or refreshing traffic preserves the selection and updates the summary, including traffic sampled while the save dialog was open.
- Exact decimal aggregation across up to 400 retained daily counters prevents integer overflow; displayed amounts use the existing rounded B/KB/MB/GB units while CSV retains exact bytes. Reading totals does not modify usage, mark pending traffic saved or change settings.

### Upgrading from 1.14.0

- Exit the old widget, then install Setup or extract the portable ZIP. Existing settings, backups and usage remain compatible.
- Open **Usage and history**, choose **CSV period**, and read **Selected period usage** below the recorded-day count. Both custom date boundaries are included; unrecorded days are not reconstructed. Scroll on smaller screens to reach the summary and export button.

## [1.14.0] - 2026-10-12

### Added

- CSV period selection in Usage and history: all retained days (the default), a recorded/current month, or an inclusive custom date range entered as `YYYY-MM-DD`.
- English/Bengali labels, selected recorded-day count, inline validation for missing/invalid/reversed dates and an explanation when no stored records match. Invalid/empty selections cannot open the save dialog or replace an existing file.
- Month/date choices survive live refresh, language changes and month rollover while the history window remains open. Exports still include the latest sampled traffic, preserve local totals/pending persistence and use the same exact-byte CSV format.
- Scrollable history content and screen-bound height/placement keep the new controls reachable on smaller displays; filter controls use native templates, system colors and accessible names/help.

### Upgrading from 1.13.0

- Exit the old widget, then install Setup or extract the portable ZIP. Settings, backups and usage stay compatible.
- Open **Usage and history**, choose **CSV period**, and select **Export CSV…**. Both date boundaries are included. Only retained records are exported; missing days are not reconstructed. Reopening the history window defaults to all retained days.

## [1.13.0] - 2026-10-12

### Added

- **Export CSV… / CSV সংরক্ষণ করুন…** in Usage and history exports all retained daily totals (up to 400 recorded days), including the latest sampled traffic awaiting normal persistence.
- Stable columns `Date,DownloadBytes,UploadBytes,TotalBytes`, ascending ISO local dates and exact byte counts independent of UI language, regional number formats or selected speed units. CSV uses comma separators, UTF-8 with BOM and CRLF lines; days without stored records are omitted.
- The export button explains empty history and becomes available when measured usage exists. Cancel leaves files and usage unchanged. File replacement is atomic; write failures preserve the previous destination and local totals, with localized error reporting.

### Upgrading from 1.12.0

- Exit the old widget, then run Setup or extract the new portable ZIP. Settings, backups and usage remain compatible.
- Open **Usage and history → Export CSV…**, choose a destination and open/import the file in a spreadsheet app. Values are bytes (`1 MiB = 1,048,576 bytes`). Export covers traffic measured while TrafficSense runs; it does not reconstruct unmeasured days or alter the stored usage history.

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

[Unreleased]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.21.0...HEAD
[1.21.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.20.0...v1.21.0
[1.20.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.19.0...v1.20.0
[1.19.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.18.1...v1.19.0
[1.18.1]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.18.0...v1.18.1
[1.18.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.17.0...v1.18.0
[1.17.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.16.0...v1.17.0
[1.16.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.15.0...v1.16.0
[1.15.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.14.0...v1.15.0
[1.14.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.13.0...v1.14.0
[1.13.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.12.0...v1.13.0
[1.12.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.11.0...v1.12.0
[1.11.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.5.0...v1.11.0
[1.5.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/almuzahidseyam/TrafficSense/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/almuzahidseyam/TrafficSense/releases/tag/v1.2.0
