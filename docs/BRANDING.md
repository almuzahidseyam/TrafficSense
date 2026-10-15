# TrafficSense branding

The cyan download and mint upload arrows on a navy tile identify TrafficSense.
The compact monitoring widget keeps its existing dimensions; the logo appears in
the executable/shortcuts, notification area, About window, About menu and installer.

`src/Assets/TrafficSense-logo.png` is the original transparent master generated
with the built-in image generation tool on 2026-10-15. The UI uses the smaller
`TrafficSense-128.png`, and Windows uses `TrafficSense.ico` (16, 20, 24, 32, 40,
48, 64, 128 and 256 pixels). Only the small PNG and ICO are embedded in the app.

Run `./scripts/Build-BrandAssets.ps1` on Windows to regenerate the derivatives.
This performs format/size conversion with alpha preservation, without redrawing
the artwork. No Python or image API dependency is required to build releases.

Generation prompt:

> Use case: logo-brand. Create one finished Windows application icon for TrafficSense, a network speed monitor. A single centered compact rounded-square deep navy tile, containing two bold clean geometric arrows: cyan downward arrow at left and mint upward arrow at right, flowing as a balanced recognizable two-way traffic mark. Professional minimal flat design, precise geometry, crisp edges, no text or letters, no thin lines, no chart clutter, no mockup, no external shadow. Generous clear separation between arrows so readable at 16 pixels. Tile occupies about 90 percent of a square canvas. Outside tile genuinely transparent. Output one icon only, straight on, suitable for Windows desktop and tray, 1024 square.
