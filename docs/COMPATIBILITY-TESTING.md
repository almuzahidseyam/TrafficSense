# Compatibility evidence and repeatable plan

Reviewed on 2026-10-13, Windows 11 build 26200. This records the cases actually exercised, not certification of every plugin, VPN or display configuration.

| Area | Test type / coverage target | Cases and evidence | Remaining gap |
| --- | --- | --- | --- |
| Native ABI/lifecycle | Compiler-built contract and process tests; all advertised API gates | API 1–8, API 9 rejection, GDI/extended drawing, theme DC state, deadlines, graceful DLL teardown and abrupt-owner-exit job cleanup | Different compiler/runtime combinations and every community callback |
| Community plugins | Explicit author-release binary tests with isolated configuration | Four pinned releases below; sampling/drawing, Cancel/OK, persistence, modal Stop/reopen; all seven TextReader commands; DateTime edited-format Cancel/OK/reload | Every option field, input events, alternate configurations and physical battery correctness |
| Modal request lifecycle | Native pumped callback and real WPF bridge controls | One callback per request under nested timer dispatch, concurrent rejection, selected command, responsive host UI, invalid indices, Stop, reopen and close cleanup | Other compiler/UI framework modal loops and callbacks |
| Finite stability | Elapsed-time integration and resource budgets | Widget visible/hidden soak plus four sampling/drawing workers; theme changes, clock progress, native handles, CPU/memory and reload cleanup; separate repeated-dialog resource trace | TextReader repeated-dialog growth warning below; hours/days, optional metrics/panel combinations and other hardware |
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

## Settings, commands and longer runs in 1.29.0

A pumped native callback reproduced an `EndOfStreamException` in the unguarded 1.28.0 worker: the timer re-entered the same pending request while its modal message loop was active. The worker now claims that request until execution returns. Compiler-built fixtures check options/commands exactly once, concurrent rejection, invalid indices and stopping a callback that keeps pumping indefinitely. The actual WPF bridge probe drives its own buttons, checks that the UI heartbeat continues, and verifies Stop/reopen/close cleanup. Default portable CI covers these original fixtures, including private-desktop launcher/child job ownership; it does not acquire third-party binaries.

[Community interaction evidence](validation/community-interactions-2026-10-13.json) covers real Cancel/OK dialogs for all four pinned binaries, normal configuration save, modal Stop and reopening. An edited DateTime time format is discarded on Cancel, accepted on OK and retained across reload. TextReader's previous/next, bookmark, auto-page, hide, chapters and bookmarks commands use only a seeded owned document. Chapter/bookmark dialogs are cancelled. Other option fields, global key toggles and battery hardware were not exercised.

Community dialogs run through a **test-only native launcher on a private Windows desktop which is never switched into view**. A handshake allows child creation only after the launcher has been assigned to the parent's kill-on-close job. The test executable verifies its desktop before sending any dialog messages; both launcher and native worker must exit during modal Stop. The production executable has no dialog-driver or launcher path. A private desktop isolates test UI/focus, not the plugin's file/network permissions.

The [widget stability report](validation/widget-soak-2026-10-13.json) and [plugin stability report](validation/plugin-soak-2026-10-13.json) record requested/actual elapsed time and all measurements. Widget measurements use the existing visible/hidden phases, normal and test-only collection snapshots and 3,000 accelerated stress iterations. The default native soak measures uninterrupted **sampling/drawing**, alternating themes and three reload/cleanup cycles each, using direct worker processes with the product resource checks. Worker regression budgets remain +32 kernel handles, +8 GDI and +8 USER objects; private-memory growth is gated at 64 MiB after warmup. No collection is forced in those workers. CPU is recorded rather than gated. Finite same-machine results are not an hours/days or hardware certification.

### Repeated dialogs: discovered boundary and recovery

The [eight-dialog resource trace](validation/plugin-dialog-resources-2026-10-13.json) distinguishes first-use Windows/MFC input/font caches from ongoing growth. DateTime, Battery and KeyboardIndicator stabilize after their first few dialogs. TextReader 1.02 continues retaining about seven GDI objects per settings dialog after warmup. Its author binary is unchanged; this is a known compatibility warning, not a clean repeated-dialog resource result. All seven commands' functional results remain valid.

The [initial interactive run](validation/plugin-soak-interactive-cold-2026-10-13.json) is retained as **failed**, not counted as a passed soak: sampling-only warmup did not initialize the subsequently measured native UI caches. An explicit interactive soak now warms modal paths too; it can still fail the strict budgets on the known TextReader growth. The default sampling-only soak is a separate workload and makes no repeated-dialog leak-free claim.

TrafficSense 1.29.0 adds **Reload plugin** in English/Bengali. After each completed response it checks the direct worker's absolute resource use: above 1,000 GDI objects, 1,000 USER objects, 2,048 kernel handles or 512 MiB private memory stops the worker with a translated recovery message. A completed callback gets up to 300 ms for normal EOF/unload/settings teardown before the job is closed. Stuck callbacks still use deadlines/Stop. Reload explicitly starts the same chosen DLL with its saved configuration; transient plugin state may reset. These are response-bound checks, not a hard memory/CPU sandbox. Direct native fixtures deliberately leak 1,200 real GDI objects to verify containment, settings teardown and recovery; actual WPF button tests verify Stop/Reload and the fault message. Stops also remove a plugin's accumulated process resources.

```powershell
# Original native modal/UI/ownership fixtures only.
./scripts/Test-PluginInteractions.ps1

# Pinned binaries, private-desktop dialog checks, resource trace and 15-minute
# sampling/drawing soak (repeated dialogs are a separate workload).
./scripts/Test-PluginInteractions.ps1 -Community -Seconds 900

# Diagnostic repeated-dialog workload. TextReader's known growth can fail this.
./scripts/Test-PluginInteractions.ps1 -Community -Seconds 900 -InteractiveSoak

# Real widget visible/hidden measurement and subsequent accelerated UI stress.
./scripts/Test-Stability.ps1 -Seconds 900 -StressIterations 3000
```

Each script creates a fresh ignored output directory. A failure or incomplete run must not be described as passed; use the report's elapsed durations, error list and coverage fields.

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
