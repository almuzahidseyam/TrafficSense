using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace TrafficSense;

public partial class MainWindow
{
    private void InitializeCustomization()
    {
        var normal = SettingsStore.Normalize(_settings); WidgetSkin.From(normal).Apply(_settings);
        foreach (var (key, skin) in new[]
        {
            (TextKey.SkinClassic, new WidgetSkin(14, normal.WidgetFontFamily, true, 120, "Auto", "Auto", "Auto", "Auto")),
            (TextKey.SkinOcean, new WidgetSkin(14, normal.WidgetFontFamily, true, 191, "#061C2D", "#45E0B6", "#60A5FA", "#F1F5F9")),
            (TextKey.SkinMono, new WidgetSkin(14, normal.WidgetFontFamily, true, 255, "#101010", "#FFFFFF", "#FFFFFF", "#FFFFFF"))
        })
        {
            var item = new MenuItem { Tag = key }; item.Click += (_, e) => { ApplySkin(skin); e.Handled = true; };
            MenuSkinPresets.Items.Add(item);
        }
        foreach (TextKey key in new[] { TextKey.BackgroundColor, TextKey.DownloadColor, TextKey.UploadColor, TextKey.MetricsColor })
        {
            var item = new MenuItem { Tag = key }; item.Click += (_, e) => { ChooseColor(key); e.Handled = true; };
            MenuColors.Items.Add(item);
        }
        foreach (int limit in new[] { 0, 1, 5, 10, 50, 100, 500, 1000 })
        {
            AddLimit(MenuDailyLimit, limit, false); AddLimit(MenuMonthlyLimit, limit, true);
        }
    }

    private void AddLimit(MenuItem menu, int limit, bool monthly)
    {
        var item = new MenuItem { Tag = limit, IsCheckable = true };
        item.Click += (_, e) => { ChangeUsageLimits(monthly ? _settings.DailyLimitGiB : limit, monthly ? limit : _settings.MonthlyLimitGiB); e.Handled = true; };
        menu.Items.Add(item);
    }

    private void ApplyCustomizationLanguage()
    {
        ApplyLayoutLanguage();
        MenuPluginBridge.Header = Localization[TextKey.PluginBridge];
        MenuSkinPresets.Header = Localization[TextKey.SkinPresets]; MenuColors.Header = Localization[TextKey.ItemColors];
        MenuFontFamily.Header = Localization[TextKey.ChooseFont]; MenuSkinImport.Header = Localization[TextKey.ImportSkin];
        MenuSkinExport.Header = Localization[TextKey.ExportSkin]; MenuDailyLimit.Header = Localization[TextKey.DailyLimit];
        MenuMonthlyLimit.Header = Localization[TextKey.MonthlyLimit]; MenuDailyLimit.ToolTip = MenuMonthlyLimit.ToolTip = Localization[TextKey.UsageLimitHint];
        MenuSensorSource.Header = Localization[TextKey.SensorSource]; MenuSensorSource.ToolTip = _settings.SensorFile ?? Localization[TextKey.SensorHint];
        MenuSensorBridge.Header = Localization[TextKey.SensorBridge];
        MenuChooseSensor.Header = Localization[TextKey.ChooseSensor]; MenuClearSensor.Header = Localization[TextKey.ClearSensor];
        MenuClearSensor.IsEnabled = _settings.SensorFile != null;
        foreach (MenuItem item in MenuSkinPresets.Items) item.Header = Localization[(TextKey)item.Tag];
        foreach (MenuItem item in MenuColors.Items) item.Header = Localization[(TextKey)item.Tag];
        foreach (var menu in new[] { MenuDailyLimit, MenuMonthlyLimit })
            foreach (MenuItem item in menu.Items)
            {
                int limit = (int)item.Tag; item.Header = limit == 0 ? Localization[TextKey.Disabled] : limit.ToString(CultureInfo.CurrentCulture) + " GiB";
                item.IsChecked = limit == (menu == MenuDailyLimit ? _settings.DailyLimitGiB : _settings.MonthlyLimitGiB);
            }
    }

    internal bool ApplySkin(WidgetSkin skin)
    {
        // Clone preserves unrelated network, history, position and panel settings.
        var candidate = SettingsStore.Normalize(_settings); skin.Apply(candidate);
        var result = _saveSettings(candidate);
        if (!result.Success) { ShowSettingsError(result.Error); return false; }
        _settings = candidate; _appearancePending = true;
        ApplyAppearanceLanguage(); QueueWidgetRender(); RefreshTaskbarPanel(); return true;
    }

    private void ApplyCustomStyle()
    {
        var family = new FontFamily(_settings.WidgetFontFamily);
        TxtDownload.FontFamily = TxtUpload.FontFamily = TxtMetrics.FontFamily = family;
        TxtSampleState.FontFamily = family;
        TxtSampleState.Foreground = _settings.Theme == "Light" ? Brushes.Black : Brushes.White;
        TxtDownload.FontWeight = TxtUpload.FontWeight = TxtMetrics.FontWeight = _settings.WidgetBold ? FontWeights.SemiBold : FontWeights.Normal;
        if (SystemParameters.HighContrast)
        {
            ((Border)Content).Background = SystemColors.WindowBrush;
            TxtDownload.Foreground = TxtUpload.Foreground = TxtMetrics.Foreground = TxtSampleState.Foreground = IconDownload.Foreground = IconUpload.Foreground = SystemColors.WindowTextBrush;
            return;
        }
        Brush? Brush(string color) => color == "Auto" ? null : new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        if (Brush(_settings.DownloadColor) is { } download) TxtDownload.Foreground = IconDownload.Foreground = download;
        else IconDownload.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
        if (Brush(_settings.UploadColor) is { } upload) TxtUpload.Foreground = IconUpload.Foreground = upload;
        else IconUpload.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));
        if (Brush(_settings.MetricsColor) is { } metrics) TxtMetrics.Foreground = TxtSampleState.Foreground = metrics;
        if (_settings.WidgetBackground != "Auto")
        {
            var color = (Color)ColorConverter.ConvertFromString(_settings.WidgetBackground);
            color.A = (byte)_settings.BackgroundOpacity; ((Border)Content).Background = new SolidColorBrush(color);
        }
    }

    private void ChooseColor(TextKey key)
    {
        using var dialog = new Forms.ColorDialog { FullOpen = true };
        if (dialog.ShowDialog(new DialogOwner(new WindowInteropHelper(this).Handle)) != Forms.DialogResult.OK) return;
        string color = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        var skin = WidgetSkin.From(_settings);
        ApplySkin(key switch { TextKey.BackgroundColor => skin with { Background = color }, TextKey.DownloadColor => skin with { Download = color },
            TextKey.UploadColor => skin with { Upload = color }, _ => skin with { Metrics = color } });
    }

    private void MenuFontFamily_Click(object sender, RoutedEventArgs e)
    {
        using var font = new System.Drawing.Font(_settings.WidgetFontFamily.Split(',')[0], _settings.WidgetFontSize * 72f / 96,
            _settings.WidgetBold ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular);
        using var dialog = new Forms.FontDialog { Font = font, ShowColor = false, MinSize = 9, MaxSize = 18, AllowScriptChange = false };
        if (dialog.ShowDialog(new DialogOwner(new WindowInteropHelper(this).Handle)) != Forms.DialogResult.OK) return;
        int size = WidgetAppearance.FontSizes.MinBy(value => Math.Abs(value - dialog.Font.SizeInPoints * 96 / 72));
        ApplySkin(WidgetSkin.From(_settings) with { FontFamily = WidgetSkin.Family(dialog.Font.FontFamily.Name), Bold = dialog.Font.Bold, FontSize = size });
    }

    internal bool ImportSkin(string path)
    {
        try { return ApplySkin(WidgetSkin.Read(path)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException or NotSupportedException)
        { ShowSettingsError(error.Message, TextKey.SkinFailed); return false; }
    }
    private void MenuSkinImport_Click(object sender, RoutedEventArgs e)
    { var dialog = new OpenFileDialog { Filter = "TrafficSense skin (*.json)|*.json", Title = Localization[TextKey.ImportSkin] }; if (dialog.ShowDialog(this) == true) ImportSkin(dialog.FileName); }
    private void MenuSkinExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "TrafficSense skin (*.json)|*.json", FileName = "TrafficSense-skin.json", Title = Localization[TextKey.ExportSkin], OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return;
        var result = WidgetSkin.From(_settings).Export(dialog.FileName); if (!result.Success) ShowSettingsError(result.Error, TextKey.SkinFailed);
    }
    private void MenuChooseSensor_Click(object sender, RoutedEventArgs e)
    { var dialog = new OpenFileDialog { Filter = "Sensor JSON (*.json)|*.json", Title = Localization[TextKey.ChooseSensor] }; if (dialog.ShowDialog(this) == true) ChangeSensorSource(dialog.FileName); }
    private void MenuClearSensor_Click(object sender, RoutedEventArgs e) => ChangeSensorSource(null);
    private void MenuSensorBridge_Click(object sender, RoutedEventArgs e)
    {
        string? executable = Environment.ProcessPath;
        if (executable == null || !string.Equals(Path.GetFileNameWithoutExtension(executable), "TrafficSense", StringComparison.OrdinalIgnoreCase))
        { ShowSettingsError(null, TextKey.BridgeLaunchFailed); return; }
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable, "--sensor-bridge") { UseShellExecute = false });
            if (process == null) { ShowSettingsError(null, TextKey.BridgeLaunchFailed); return; }
            ChangeSensorSource(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrafficSense", "sensors.json"));
            ChangeDisplayedMetrics((SystemMetric)_settings.DisplayMetrics | SystemMetric.Sensors);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        { ShowSettingsError(error.Message, TextKey.BridgeLaunchFailed); }
    }
    internal bool ChangeSensorSource(string? path)
    {
        string? previous = _settings.SensorFile; _settings.SensorFile = ExternalSensorFile.NormalizePath(path);
        if (path != null && _settings.SensorFile == null) { _settings.SensorFile = previous; ShowSettingsError(null, TextKey.SensorUnavailable); return false; }
        var result = _saveSettings(_settings); if (!result.Success) { _settings.SensorFile = previous; ShowSettingsError(result.Error); }
        ConfigureSystemMetrics(); ApplyCustomizationLanguage(); return result.Success;
    }
    private sealed class DialogOwner(IntPtr handle) : Forms.IWin32Window { public IntPtr Handle => handle; }
}
