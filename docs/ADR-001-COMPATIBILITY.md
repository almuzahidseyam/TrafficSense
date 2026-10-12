# ADR-001: Compatibility without coupling failures to speed collection

**Status:** Accepted
**Date:** 2026-10-13
**Deciders:** TrafficSense maintainer, through the authorized compatibility development request

## Context

TrafficMonitor provides native C++ DLL plugins, positioned bitmap/XML/INI skins and taskbar embedding. TrafficSense uses managed WPF and independently sampled counters. The existing sensor-file bridge does not implement native DLL compatibility. Explorer internals and plugin hardware dependencies vary by machine.

## Decision

Implement the documented x64 virtual-call plugin ABI in an explicitly started worker process, with bounded responses, deadlines, kernel job ownership and user-visible failures. Do not load third-party code into the monitor. Read legacy INI/XML layouts independently, bound local files and images, preserve PNG alpha and render classic BMP backgrounds opaquely. Offer experimental taskbar embedding only when a host layout can be identified and enough unoccupied space exists; retain the appbar fallback and remove owned attachments on shutdown.

## Options considered

| Option | Complexity | Resource / maintenance cost | Assessment |
| --- | --- | --- | --- |
| Load DLLs into WPF | Lower initial effort | Native faults/hangs terminate monitoring | Rejected |
| Separate plugin worker | Moderate | One extra process while explicitly connected | Chosen; crash containment, not a security sandbox |
| Translate every plugin into sensor JSON | Moderate per plugin | Requires plugin-specific rewrites | Retain as an alternative, not DLL compatibility |
| Modify Explorer's button layout or inject code | High | Windows-version-sensitive global shell changes | Rejected |
| Native child in verified vacant taskbar space, appbar fallback | Moderate/high | Requires layout/DPI recovery and physical validation | Chosen opt-in; do not overlap buttons or resize Explorer children |

## Consequences

- A plugin can still exercise the current user's permissions; only trusted DLLs should be selected. Process separation limits accidental crash/hang impact, not malicious code.
- Versioned ABI capabilities must be checked before newer slots are called. Unsupported capabilities must be explicit; no fabricated readings.
- Skin assets need a managed local copy so moving the original download does not break the widget. Layout imports and appearance-only JSON remain separate formats.
- Physical NIC/VPN and multi-monitor validation must distinguish available evidence from untested combinations. Automated tests must not reconfigure a user's real NICs, suspend the PC or restart Explorer.

## Action items

1. Implement bounded legacy layout/image import and native rendering tests.
2. Implement worker ABI, plugin preview/controls, deadlines and crash/hang fixtures.
3. Implement taskbar host discovery, vacant-region placement and fallback/recovery tests.
4. Record same-interface native counter comparison and available display evidence; publish reproducible tools for additional devices.
5. Improve grouped settings, onboarding and presentation, then run package/release checks.

References: [upstream interface](https://github.com/zhongyang219/TrafficMonitor/blob/master/include/PluginInterface.h), [skin documentation](https://github.com/zhongyang219/TrafficMonitor/wiki/%E7%9A%AE%E8%82%A4%E5%88%B6%E4%BD%9C%E6%95%99%E7%A8%8B), [SetParent/DPI constraints](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent). These are format/API references; upstream source and artwork are not redistributed.
