using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace TrafficSense;

public partial class MainWindow
{
    private LegacySkin? _layoutSkin;
    private readonly List<(LegacySkinItem Item, TextBlock Label, TextBlock Value)> _layoutFields = [];
    private readonly Dictionary<string, Image> _layoutImages = [];
    private static BitmapSource? SkinBackground(LegacySkin skin)
    {
        if (skin.Background == null) return null;
        byte[] data = LegacySkin.ReadBounded(skin.Background, 8 * 1024 * 1024);
        // Check image dimensions before asking an image decoder to allocate pixels.
        int width, height;
        if (data.Length >= 24 && data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        { width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16, 4)); height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20, 4)); }
        else if (data.Length >= 54 && data[0] == 'B' && data[1] == 'M' && BitConverter.ToInt32(data, 14) >= 40)
        { width = BitConverter.ToInt32(data, 18); height = BitConverter.ToInt32(data, 22); if (height < 0 && height != int.MinValue) height = -height; }
        else throw new InvalidDataException("Unsupported skin bitmap.");
        if (width is < 1 or > 2048 || height is < 1 or > 2048) throw new InvalidDataException("Skin bitmap dimensions exceed 2048 pixels.");
        using var stream = new MemoryStream(data);
        var decoded = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        if (decoded.PixelWidth != width || decoded.PixelHeight != height) throw new InvalidDataException("Inconsistent skin bitmap.");
        BitmapSource result = decoded;
        if (Path.GetExtension(skin.Background).Equals(".bmp", StringComparison.OrdinalIgnoreCase))
        {
            var bgra = new FormatConvertedBitmap(decoded, PixelFormats.Bgra32, null, 0); int stride = width * 4;
            var pixels = new byte[stride * height]; bgra.CopyPixels(pixels, stride, 0);
            // Classic BMP backgrounds are opaque; alpha is a PNG capability.
            for (int i = 0; i < pixels.Length; i += 4) pixels[i + 3] = 255;
            result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        }
        result.Freeze(); return result;
    }

    internal bool ChangeLayoutSkin(string? path, bool compact, string? managedRoot = null)
    {
        try
        {
            LegacySkin? skin = path == null ? null : LegacySkin.Load(path, compact); if (skin != null) SkinBackground(skin);
            var candidate = SettingsStore.Normalize(_settings);
            if (path != null)
            {
                string folder = Path.Combine(managedRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrafficSense", "skins"), Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);
                string target = Path.Combine(folder, Path.GetFileName(path)); File.WriteAllBytes(target, LegacySkin.ReadBounded(path, 65536));
                // Copy both variants so switching small/large doesn't depend on a download folder.
                foreach (string name in new[] { "background.png", "background_l.png", "background.bmp", "background_l.bmp" })
                {
                    string source = Path.Combine(Path.GetDirectoryName(path)!, name);
                    if (File.Exists(source)) File.WriteAllBytes(Path.Combine(folder, name), LegacySkin.ReadBounded(source, 8 * 1024 * 1024));
                }
                candidate.LayoutSkinFile = target;
                SkinBackground(LegacySkin.Load(target, compact));
            }
            else candidate.LayoutSkinFile = null;
            candidate.LayoutSkinCompact = compact;
            var saved = _saveSettings(candidate); if (!saved.Success) { ShowSettingsError(saved.Error); return false; }
            _settings = candidate; _appearancePending = true; ApplyPendingAppearance(); ConfigureSystemMetrics(); return true;
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Xml.XmlException or System.FormatException)
        { ShowSettingsError(e.Message, TextKey.SkinFailed); return false; }
    }

    private void ApplyLayoutSkin()
    {
        _layoutSkin = null; _layoutFields.Clear(); _layoutImages.Clear(); LayoutCanvas.Children.Clear();
        try { if (_settings.LayoutSkinFile != null) _layoutSkin = LegacySkin.Load(_settings.LayoutSkinFile, _settings.LayoutSkinCompact); }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Xml.XmlException or System.FormatException)
        { _diagnostics?.Write("LayoutSkin", e); }
        var border = (Border)Content; var skin = _layoutSkin;
        BitmapSource? background = null;
        try { if (skin != null) background = SkinBackground(skin); }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.FormatException)
        { _diagnostics?.Write("LayoutBitmap", e); _layoutSkin = skin = null; }
        NormalSurface.Visibility = skin == null ? Visibility.Visible : Visibility.Collapsed;
        LayoutSurface.Visibility = skin == null ? Visibility.Collapsed : Visibility.Visible;
        border.Padding = skin == null ? new Thickness(8, 4, 8, 4) : new Thickness(0);
        if (skin == null) return;
        border.Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : Brushes.Transparent;
        LayoutCanvas.Width = LayoutStatus.MaxWidth = skin.Width; LayoutCanvas.Height = skin.Height;
        LayoutCanvas.Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : background == null ? Brushes.Transparent : new ImageBrush(background) { Stretch = Stretch.Fill, Opacity = _settings.BackgroundOpacity / 255d };
        foreach (var item in skin.Items)
        {
            Brush brush = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : new SolidColorBrush(Color.FromRgb((byte)item.Color, (byte)(item.Color >> 8), (byte)(item.Color >> 16)));
            var label = new TextBlock(); var value = new TextBlock();
            foreach (var text in new[] { label, value })
            {
                text.Foreground = brush; text.FontFamily = new FontFamily(skin.Font); text.FontSize = skin.FontSize;
                text.FontWeight = (skin.Style & 1) != 0 ? FontWeights.Bold : FontWeights.Normal;
                text.FontStyle = (skin.Style & 2) != 0 ? FontStyles.Italic : FontStyles.Normal;
                text.TextDecorations = new TextDecorationCollection();
                if ((skin.Style & 4) != 0) text.TextDecorations.Add(TextDecorations.Underline[0]);
                if ((skin.Style & 8) != 0) text.TextDecorations.Add(TextDecorations.Strikethrough[0]);
            }
            var container = new Grid { Width = item.Width, Height = skin.TextHeight, ClipToBounds = true };
            if (item.Align == 3) { label.HorizontalAlignment = HorizontalAlignment.Left; value.HorizontalAlignment = HorizontalAlignment.Right; container.Children.Add(label); }
            else value.TextAlignment = item.Align == 1 ? TextAlignment.Right : item.Align == 2 ? TextAlignment.Center : TextAlignment.Left;
            container.Children.Add(value); Canvas.SetLeft(container, item.X); Canvas.SetTop(container, item.Y); LayoutCanvas.Children.Add(container);
            if (item.PluginId != null) { var image = new Image { Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed }; _layoutImages[item.Key] = image; container.Children.Add(image); }
            _layoutFields.Add((item, label, value));
        }
        RenderLayoutSkin();
    }
    private void RenderLayoutSkin()
    {
        if (_layoutSkin == null) return;
        var metrics = _systemService?.Latest ?? new();
        foreach (var (item, label, text) in _layoutFields)
        {
            string value = item.PluginId != null ? PluginValue(item.PluginId) : item.Key switch
            {
                "up" => TxtUpload.Text, "down" => TxtDownload.Text,
                "total_speed" => SpeedFormatter.Format(_latestSpeed.DownloadBytesPerSecond + _latestSpeed.UploadBytesPerSecond, _settings.SpeedInBits),
                "today_traffic" => HistoryWindow.FormatBytes((decimal)_usage.Today(DateOnly.FromDateTime(_localTime().Date)).Received + _usage.Today(DateOnly.FromDateTime(_localTime().Date)).Sent),
                "cpu" => MetricText(SystemMetric.Cpu, metrics), "memory" => MetricText(SystemMetric.Memory, metrics),
                "gpu" => MetricText(SystemMetric.Gpu, metrics), "hdd" => MetricText(SystemMetric.Disk, metrics), "cpu_freq" => MetricText(SystemMetric.Frequency, metrics),
                _ => Localization[TextKey.NotAvailable]
            };
            string caption = _layoutSkin.NoLabel ? "" : item.Label.Length > 0 ? item.Label : item.Key + ": ";
            label.Text = caption; text.Text = item.Align == 3 ? value : caption + value;
            if (_layoutImages.TryGetValue(item.Key,out var image))
            {
                image.Source = PluginImageFor(item.PluginId!); image.Visibility = image.Source == null ? Visibility.Collapsed : Visibility.Visible;
                text.Visibility = label.Visibility = image.Source == null ? Visibility.Visible : Visibility.Collapsed;
                System.Windows.Automation.AutomationProperties.SetName(image,caption + value);
            }
            if (item.Align == 3 && (image == null || image.Source == null))
            {
                label.Visibility = Visibility.Visible; text.HorizontalAlignment = HorizontalAlignment.Right;
                label.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity)); text.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
                if (label.DesiredSize.Width + text.DesiredSize.Width >= item.Width)
                { label.Visibility = Visibility.Collapsed; text.HorizontalAlignment = HorizontalAlignment.Stretch; text.Text = caption + value; }
            }
            System.Windows.Automation.AutomationProperties.SetName(text, caption + value);
        }
        LayoutStatus.Text = _sampleQuality == SampleQuality.Fresh ? "" : Localization[_sampleQuality == SampleQuality.Unavailable ? TextKey.SampleUnavailableShort : _sampleQuality == SampleQuality.Delayed ? TextKey.SampleDelayedShort : TextKey.SamplingShort];
        LayoutStatus.Foreground = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Brushes.OrangeRed;
        LayoutStatus.Visibility = LayoutStatus.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
    private void ApplyLayoutLanguage()
    {
        MenuLayoutSkin.Header = Localization[TextKey.LayoutSkin]; MenuLayoutImport.Header = Localization[TextKey.LayoutImport];
        MenuLayoutCompact.Header = Localization[TextKey.LayoutCompact]; MenuLayoutCompact.IsChecked = _settings.LayoutSkinCompact;
        MenuLayoutClear.Header = Localization[TextKey.LayoutClear]; MenuLayoutClear.IsEnabled = MenuLayoutCompact.IsEnabled = _settings.LayoutSkinFile != null;
    }
    private void MenuLayoutImport_Click(object sender, RoutedEventArgs e)
    { var dialog = new OpenFileDialog { Filter = "TrafficMonitor skin (*.ini;*.xml)|*.ini;*.xml", Title = Localization[TextKey.LayoutImport] }; if (dialog.ShowDialog(this) == true) { ChangeLayoutSkin(dialog.FileName, _settings.LayoutSkinCompact); ApplyLayoutLanguage(); } }
    private void MenuLayoutCompact_Click(object sender, RoutedEventArgs e) { ChangeLayoutSkin(_settings.LayoutSkinFile, !_settings.LayoutSkinCompact); ApplyLayoutLanguage(); }
    private void MenuLayoutClear_Click(object sender, RoutedEventArgs e) { ChangeLayoutSkin(null, false); ApplyLayoutLanguage(); }
}
