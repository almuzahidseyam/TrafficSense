# Sensor extensions

TrafficSense accepts a **data-only sensor file**; a provider runs separately and can supply temperature, fan speed or other values. The monitor never loads a provider DLL or executes code named in the file. TrafficMonitor native DLL plugins are not compatible with this protocol.

## LibreHardwareMonitor bridge

1. Obtain [LibreHardwareMonitor from its official repository](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor). Run it and enable its web server using that application's controls. Hardware availability and any required privileges belong to that provider; TrafficSense does not install its drivers or elevate it.
2. In the packaged TrafficSense app choose **Sensor extension → Open sensor bridge**. A separate process/window opens. Enter the provider's local port (commonly 8085) and click **Connect**. The widget selects the bridge's data file and enables External sensors.
3. The bridge reads `http://127.0.0.1:<port>/data.json` once per second and takes up to eight numeric `Temperature` nodes with `SensorId`, `RawValue`, `Text` and `Children`. It uses Celsius `RawValue`, not localized display strings or Fahrenheit formatting. The provider's [HTTP implementation](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LibreHardwareMonitor.Windows.Forms/Utilities/HttpServer.cs) defines this schema. Older versions lacking `RawValue` are unsupported. Authenticated endpoints need a separate compatible file provider; the included bridge does not store credentials.
4. Disconnect or close the bridge to stop it. After five seconds the last published sample becomes unavailable. Close both the widget and bridge before upgrading/uninstalling.

The bridge uses the same packaged executable in `--sensor-bridge` mode, with a separate per-user single-instance name. It never sends a request outside loopback, follows redirects or uses a proxy. The provider remains responsible for sensor accuracy and refresh frequency: a fresh HTTP response does not prove that a physical sensor has updated.

## File contract (version 1)

Choose a local source through **Sensor extension → Choose sensor JSON**, then enable **Display system metrics → External sensors**. The bridge writes `%LOCALAPPDATA%\TrafficSense\sensors.json`; custom providers may use another local drive path.

```json
{
  "Format": "TrafficSense.Sensors",
  "Version": 1,
  "UpdatedAt": "2026-10-13T00:00:00Z",
  "Sensors": [
    { "Id": "cpu-package", "Label": "CPU package", "Value": 42.5, "Unit": "°C" }
  ]
}
```

The example value is illustrative, not a live measurement. Publish real readings and a current UTC timestamp at least once a second. Use a unique temporary file and atomic replacement. Requirements:

- UTF-8 JSON, maximum 64 KiB and depth 8; 1–8 sensors with unique IDs.
- ID/label at most 48 characters, unit at most 16; no control characters.
- Finite numeric values with absolute magnitude at most 10¹². Units and labels are supplied by the provider, not inferred by the monitor.
- Publication timestamp no older than five seconds and no more than one second ahead of the reader's clock.
- Missing/unreadable/invalid/stale files are unavailable. A valid measured zero remains zero. Clearing the configured source stops reading it.

Keep the provider outside the main monitor process. It may use its own APIs or libraries and must publish only values it actually receives. Crash/hang recovery, sensor selection, hardware drivers and elevated access belong to that provider. Settings backups include the selected source path, not the sensor file or third-party program.
