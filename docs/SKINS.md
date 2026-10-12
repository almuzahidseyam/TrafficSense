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

This format does not import TrafficMonitor bitmap skins or arbitrary layouts. Export the existing style before changing it, or choose Classic to return to automatic theme colors.
