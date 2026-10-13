# Compatibility evidence and repeatable plan

Reviewed on 2026-10-13, Windows 11 build 26200. This records the cases actually exercised, not certification of every plugin, VPN or display configuration.

| Area | Test type / coverage target | Cases and evidence | Remaining gap |
| --- | --- | --- | --- |
| Native ABI/lifecycle | Compiler-built contract and process tests; all advertised API gates | API 1–8, API 9 rejection, GDI/extended drawing, theme DC state, deadlines, graceful DLL teardown and abrupt-owner-exit job cleanup | Different compiler/runtime combinations and every community callback |
| Community plugins | Explicit author-release binary tests with isolated configuration | Four pinned releases below; four timed samples plus 32 stress samples each; dark/light foreground, IDs, clock progress, sample-file rendering and native handle budgets | Dialogs/commands/input events, alternate configurations and physical battery correctness |
| Live VPN | Passive integration against the same interface's native counters | Four intervals, 11 frames each, simultaneous Auto/manual WARP/manual physical lanes; native brackets, exact bytes, Auto/manual equality | Physical reconnect, other VPN products, controlled LAN throughput and split-route attribution |
| VPN transitions | Deterministic service regression at every interval | Route switch, disconnected manual adapter, low reset counter, reconnect baseline, exact usage drain and no inherited counter spikes | Real driver/route notification timing |
| Display/DPI | Actual invisible WPF/native-window checks, synthetic work areas | Native context 1 before / Per-Monitor V2 after; 100/125/150/200% simulated size factors; left/above positions, removal/shrink and panel monitor/top/font changes | Physical second monitor, actual mixed-DPI transitions/hot-plug and Explorer restart |
| Placement arithmetic | Property/invariant matrix | 5 topologies × 4 size factors × 56 starting positions: fully visible placement on a real work area | Does not simulate the Windows compositor or EDID |

## Community binary scope

Author repository: [TrafficMonitorPlugins](https://github.com/zhongyang219/TrafficMonitorPlugins). The selected source snapshots were inspected before running their binaries in owned workers. The acquisition script pins tag revisions, archive hashes and DLL hashes, and rejects changed bytes. Hashes identify tested files; they are not an Authenticode or reproducible-build attestation. Third-party code retains the user's permissions; process separation is not a security sandbox.

| Plugin / release | Exercised | Result |
| --- | --- | --- |
| [DateTime 1.00](https://github.com/zhongyang219/TrafficMonitorPlugins/releases/tag/DateTime_V1.00) | Date/time text, stable IDs, advancing seconds, repeated sampling and normal configuration save | Passed |
| [Battery 1.03](https://github.com/zhongyang219/TrafficMonitorPlugins/releases/tag/Battery_V1.03) | Default icon/text preview, dark/light foreground and finite resource checks | Passed; desktop environment is not battery-hardware validation |
| [KeyboardIndicator 1.00](https://github.com/zhongyang219/TrafficMonitorPlugins/releases/tag/KeyboardIndicator_V1.00) | Default custom drawing, dark/light readability and finite resources | Passed; real key toggles were not changed |
| [TextReader 1.02](https://github.com/zhongyang219/TrafficMonitorPlugins/releases/tag/TextReader_V1.02) | Owned UTF-8 sample file through isolated INI configuration, custom drawing and finite resources | Passed; no user's document opened |

[Community raw report](validation/community-plugins-2026-10-13.json) records exact versions/hashes, API 6 items and resource snapshots. The plugins are not bundled with TrafficSense and no automatic download/discovery is added to normal startup.

## Available live hardware

The available PC has a running Cloudflare WARP tunnel, physical Ethernet and one 1920×1200 monitor at 96 DPI. IPv4 route lookups for five public destinations selected the tunnel; these lookups do not send packets. Both the tunnel and its physical carrier advanced during the passive tests.

At every interval, Auto's observed bytes equaled manual selection of that same tunnel, while adding the physical interface would produce a different total. Direct compiled `GetIfEntry2` reads bracketed managed reads, and all selected byte deltas were conserved exactly. This demonstrates the tested counter/selection path, not application payload speed, ISP totals, universal VPN detection or a comparison against another monitor's accuracy. Auto still chooses one routed interface; split routes may require manual selection.

[Live report](validation/vpn-environment-2026-10-13.json) omits interface IDs, local IPs, adapter names and MAC addresses. [Display report](validation/display-topology-2026-10-13.json) records real native context/coordinates with **simulated** work areas and panel reservations. No PC sleep, VPN toggle, route/display reconfiguration, Explorer restart or existing widget activation was performed.

## Repeat on another Windows environment

The .NET 8 SDK plus Visual Studio C++ Build Tools/Windows SDK or MinGW-w64 are required for these developer probes. End users need neither. From a PowerShell prompt at the repository root:

```powershell
# Own native fixtures and invisible synthetic topology tests only.
./scripts/Test-Compatibility.ps1

# Explicitly acquire/run the four pinned third-party releases and inspect current
# live interfaces. Review the plugin source and its permissions first.
./scripts/Test-Compatibility.ps1 -Community -LiveNetwork
```

Reports go to a fresh `.artifacts/compatibility/<id>` folder. A previously acquired local manifest may be passed with `-CommunityManifest`. A live interface disappearing or route changing mid-case fails that steady-state run rather than labelling incomplete observations passed. An environment with no WARP only exercises available interfaces; check the presence/role fields before interpreting it as a VPN result.

For the remaining physical matrix, record Windows build, NIC/driver and VPN version, actual monitor count/scales, routing mode, refresh interval and enabled metrics. Repeat before/during/after reconnect and physical monitor removal in a suitable test environment. Controlled LAN payload tests need a separate endpoint. Keep those results distinct from simulations and passive counter agreement.
