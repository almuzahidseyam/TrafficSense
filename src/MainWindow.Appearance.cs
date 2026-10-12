using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace TrafficSense;

public partial class MainWindow
{
    private bool _appearancePending = true;
    private void InitializeAppearanceMenus()
    {
        foreach (int size in WidgetAppearance.FontSizes)
        {
            var item = new MenuItem { Tag = size, IsCheckable = true };
            item.Click += (_, e) =>
            {
                ChangeAppearance(size, _settings.BackgroundOpacity);
                e.Handled = true;
            };
            MenuFontSize.Items.Add(item);
        }
        foreach (int opacity in WidgetAppearance.BackgroundOpacities)
        {
            var item = new MenuItem { Tag = opacity, IsCheckable = true };
            item.Click += (_, e) =>
            {
                ChangeAppearance(_settings.WidgetFontSize, opacity);
                e.Handled = true;
            };
            MenuTransparency.Items.Add(item);
        }
    }

    private void ApplyAppearanceLanguage()
    {
        MenuFontSize.Header = Localization[TextKey.TextSize];
        MenuTransparency.Header = Localization[TextKey.BackgroundTransparency];
        foreach (MenuItem item in MenuFontSize.Items)
        {
            int size = (int)item.Tag;
            item.Header = size.ToString(CultureInfo.CurrentCulture)
                + (size == WidgetAppearance.DefaultFontSize ? $" ({Localization[TextKey.DefaultAppearance]})" : "");
        }
        foreach (MenuItem item in MenuTransparency.Items)
        {
            int opacity = (int)item.Tag;
            item.Header = opacity == WidgetAppearance.DefaultBackgroundOpacity ? Localization[TextKey.DefaultAppearance]
                : (100 - (int)Math.Round(opacity * 100d / 255)).ToString(CultureInfo.CurrentCulture) + "%";
        }
        UpdateAppearanceChecks();
    }

    private void UpdateAppearanceChecks()
    {
        foreach (MenuItem item in MenuFontSize.Items)
            item.IsChecked = (int)item.Tag == _settings.WidgetFontSize;
        foreach (MenuItem item in MenuTransparency.Items)
            item.IsChecked = (int)item.Tag == _settings.BackgroundOpacity;
    }

    private void ApplyPendingAppearance()
    {
        if (!_appearancePending) return;
        TxtDownload.FontSize = TxtUpload.FontSize = _settings.WidgetFontSize;
        TxtMetrics.FontSize = _settings.WidgetFontSize;
        IconDownload.FontSize = IconUpload.FontSize = _settings.WidgetFontSize + 2;
        // Only the background alpha changes; text, arrows, menus and dialogs
        // retain full opacity. SizeChanged keeps the enlarged widget on screen.
        ApplyTheme();
        ApplyLayoutSkin();
        _appearancePending = false;
        UpdateAppearanceChecks();
    }

    private void ChangeAppearance(int fontSize, int backgroundOpacity)
    {
        int previousSize = _settings.WidgetFontSize, previousOpacity = _settings.BackgroundOpacity;
        _settings.WidgetFontSize = WidgetAppearance.NormalizeFontSize(fontSize);
        _settings.BackgroundOpacity = WidgetAppearance.NormalizeBackgroundOpacity(backgroundOpacity);
        if (_settings.WidgetFontSize == previousSize && _settings.BackgroundOpacity == previousOpacity)
        {
            UpdateAppearanceChecks();
            return;
        }
        var result = _saveSettings(_settings);
        if (!result.Success)
        {
            _settings.WidgetFontSize = previousSize;
            _settings.BackgroundOpacity = previousOpacity;
            UpdateAppearanceChecks();
            ShowSettingsError(result.Error);
            return;
        }
        _appearancePending = true;
        UpdateAppearanceChecks();
        QueueWidgetRender();
    }
}
