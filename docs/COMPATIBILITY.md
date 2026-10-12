# Feature and compatibility comparison

Reviewed on 2026-10-13 against the [official TrafficMonitor README](https://github.com/zhongyang219/TrafficMonitor/blob/master/README_en-us.md). This compares documented capabilities, not benchmarks or an endorsement. TrafficSense is independently implemented.

| Capability | TrafficSense 1.27 | Boundary |
| --- | --- | --- |
| Network upload/download | Background native interface sampling; 250/500/1000/2000 ms; bytes or bits | Interval average; driver batching and interface overhead apply |
| Auto/manual connections | One routed connection or selected adapter; IPv6 fallback | No per-process attribution or automatic sum across split routes |
| Floating widget / tray | Yes; lock/reset, startup, duplicate-instance activation | Startup is per executable path |
| Taskbar display | Edge appbar plus opt-in experimental native child in verified vacant taskbar space | Accessibility/Windows layout must permit placement; otherwise edge fallback. No shell injection or button resizing; physical mixed-DPI/Explorer coverage remains incomplete |
| CPU / RAM / frequency | Optional native Windows counters and physical memory | Reported frequency is an aggregate, not every core's instantaneous clock |
| GPU / disk | Optional busiest GPU engine and total physical-disk activity/read/write | Driver/Windows PDH availability varies; unavailable is not zero |
| Temperature | Separate LibreHardwareMonitor bridge or data-only provider | Requires an existing compatible provider; no bundled hardware drivers; provider controls hardware coverage/accuracy |
| Fonts / colors / skins | Local fonts/colors, presets, JSON appearance plus positioned INI/XML PNG/BMP layouts | Bounded format support, not pixel-identical rendering; no arbitrary layout editor; see [skin limits](SKINS.md) |
| Plugins | Data-only sensors and explicit x64 native ABI worker; text/GDI/extended drawing/settings/commands | API 1–8; no auto-discovery/store, input-event forwarding, resource overlays or sandbox claim; see [tested ABI and limits](NATIVE-PLUGINS.md) |
| Traffic history | 400 retained days; date/month filters, totals, daily table/chart and exact CSV | Only observed running intervals; no packet capture, application accounting or ISP billing reconciliation |
| Alerts | Daily/monthly combined-usage GiB crossings | No CPU/temperature threshold rules or notification delivery guarantee |
| Language / accessibility | English/বাংলা, keyboard controls, named controls, high contrast | Full screen-reader and physical mixed-DPI validation still needed |
| Distribution | Self-contained Windows x64 ZIP and per-user installer/uninstaller | No native ARM64/x86 package, Store/winget entry or Authenticode signing claim |

TrafficMonitor has a mature skin/plugin ecosystem and more hardware/community coverage. TrafficSense now addresses many common monitoring tasks, but this table does not claim complete parity or that either product is more accurate.

## Tested boundaries

Pure tests cover counter timing, byte conservation, resets, selection, persistence, exact exports and bounded inputs. Windows probes exercise actual WPF windows, native counters and process startup. Taskbar registration/position/fullscreen/Explorer notifications use an injected shell boundary so tests do not resize the user's desktop. Tests simulate suspend/reconnect notifications; they do not suspend the user's machine or change its adapters.

Before calling a hardware/OS combination supported in detail, record Windows build, scale, adapter/driver, VPN route, refresh interval, enabled metrics and a reproducible observation. Windows 10/11 are the package targets, not a claim that every version/Explorer customization/driver has been physically tested.

## Remaining development

1. Physical Windows 10/11, mixed-DPI, monitor removal, Explorer restart and fullscreen validation; capture results before broad shell-compatibility claims.
2. Controlled LAN transfers and concurrent VPN/split-route traces against the **same interface** counters, plus long-duration runs with optional metrics/panel enabled.
3. Expand real community-plugin and legacy skin corpus coverage; add input-event/resource-overlay integration and additional language/hardware providers while preserving process boundaries and licensing.
4. Signed distribution, ARM64 packaging and package-manager submissions after their requirements and signing ownership are established.

These remain visible gaps, not silently completed roadmap items. No repository popularity or download outcome is guaranteed by a feature release.

## Recorded local environment

On 2026-10-13 (Asia/Dhaka), Windows 11 build 26200, a passive 10.3-second run checked 21 samples against independently compiled direct `GetIfEntry2` reads surrounding each managed read. All counter brackets matched; 89,070 received and 17,645 sent bytes conserved exactly. Three connected interfaces were enumerated, one routed interface selected. One 1920×1200 display was present; a read-only taskbar inspection found vacant space. No taskbar child was attached to Explorer and no adapter/route/display setting was changed. [Raw anonymized report](validation/live-environment-2026-10-13.json).

This verifies the available same-interface counter path, not controlled payload throughput, active VPN reconnect, a physical second monitor or universal taskbar support. To repeat on another setup, compile the test fixture and run `--live-environment <fixture.dll> <report.json>` using the lifecycle test executable. Share reports only after reviewing them; the diagnostic excludes interface IDs, names and IP addresses.
