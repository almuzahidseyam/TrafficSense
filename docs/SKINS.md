# Appearance skins

Use Appearance presets, Font and Item colors from the widget menu. Export skin writes only appearance preferences; import leaves position, network selection, history, alerts and panel enablement alone. A skin is data, never executable code or a remote font URL. High contrast uses Windows system colors instead.

```json
{
  "Format": "TrafficSense.Skin",
  "Version": 1,
  "Appearance": {
    "FontSize": 14,
    "FontFamily": "Segoe UI, Nirmala UI, Kalpurush",
    "Bold": true,
    "BackgroundOpacity": 191,
    "Background": "#061C2D",
    "Download": "#45E0B6",
    "Upload": "#60A5FA",
    "Metrics": "#F1F5F9"
  }
}
```

Colors are `Auto` or uppercase `#RRGGBB`. Font sizes are 12, 14, 16, 18, 20 or 24; background alpha is 26, 64, 120 (default), 128, 191 or 255. Local family names/fallback lists use letters, digits, spaces, commas and hyphens. Imported files must contain all eight appearance properties, no unknown/duplicate properties, and fit in 16 KiB. Missing fonts use WPF's local fallback. The taskbar-edge panel remains opaque for readability.

JSON is an appearance-only exchange format. Export the existing style before changing it, or choose Classic to return to automatic theme colors.

## Positioned INI/XML bitmap layouts

Choose **Widget appearance & position → TrafficMonitor layout skin → Import INI / XML skin** and select a local `skin.ini` or `skin.xml` in its original folder. Switch **Compact layout** for the small variant, or **Use standard widget** to return. The original [Mint example](../examples/skins/Mint) is independently authored and included under this repository's license.

- Reads the documented large/small dimensions, item x/y/width/alignment, text height, label visibility, local font/point size/style and COLORREF decimal colors. Alignment 0/1/2/3 means left/right/center/label-left-value-right. INI supports the four original up/down/CPU/RAM items; XML also supports GPU, disk, frequency, total speed and today's traffic.
- Background files are `background_l.png` / `background.png`, with corresponding `.bmp` fallback. PNG preserves alpha; classic BMP is opaque. Background transparency changes the image, not text readability. Dimensions are limited to 2048 pixels before decoding, images to 8 MiB and layouts to 64 KiB/64 visible fields. XML DTD/external entities are prohibited. Files use UTF-8 or BOM-marked UTF-16; legacy ANSI encodings need conversion.
- XML `plugin_map` entries bind exact plugin item IDs from the explicitly running native bridge. Text and bounded custom-draw images are supported. Missing/stale plugin data and unbound thermal fields show unavailable. No guessed CPU/GPU temperature association is made.
- Import copies layout/background files to `%LOCALAPPDATA%\TrafficSense\skins\<id>`. Moving the download folder does not break the imported layout. Failed validation/save preserves current preferences; a missing managed asset on restart falls back to the standard widget and logs the error. Unreferenced import folders are retained for manual recovery rather than deleting user files.
- Settings backups record the managed path and compact choice, not bitmap contents. To move to another PC, copy the original skin folder and import it again. Appearance JSON export does not export a positioned layout. Sampling/unavailable/delayed status stays visible below the skin; high contrast overrides its colors/background.

This is independent format support, not pixel-identical rendering or a skin editor. Plugin-specific color ordering, legacy ANSI fonts/encodings and arbitrary executable skin extensions are not supported. Upstream source/header/artwork is not bundled.
