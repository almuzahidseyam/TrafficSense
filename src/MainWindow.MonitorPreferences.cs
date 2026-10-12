using System.Windows.Controls;

namespace TrafficSense;

public partial class MainWindow
{
    private void InitializeMonitorMenus()
    {
        foreach (bool bits in new[] { false, true })
        {
            var item = new MenuItem { Tag = bits, IsCheckable = true };
            item.Click += (_, e) => { ChangeMonitorPreferences(bits, _settings.RefreshMilliseconds); e.Handled = true; };
            MenuSpeedUnit.Items.Add(item);
        }
        foreach (int interval in MonitorPreferences.Intervals)
        {
            var item = new MenuItem { Tag = interval, IsCheckable = true };
            item.Click += (_, e) => { ChangeMonitorPreferences(_settings.SpeedInBits, interval); e.Handled = true; };
            MenuRefreshInterval.Items.Add(item);
        }
    }

    private void ApplyMonitorLanguage()
    {
        MenuSpeedUnit.Header = Localization[TextKey.SpeedUnit];
        MenuRefreshInterval.Header = Localization[TextKey.RefreshInterval];
        foreach (MenuItem item in MenuSpeedUnit.Items)
        {
            item.Header = Localization[(bool)item.Tag ? TextKey.BitsPerSecond : TextKey.BytesPerSecond];
            item.IsChecked = (bool)item.Tag == _settings.SpeedInBits;
        }
        foreach (MenuItem item in MenuRefreshInterval.Items)
        {
            item.Header = string.Format(Localization[TextKey.Seconds], (int)item.Tag / 1000d);
            item.IsChecked = (int)item.Tag == _settings.RefreshMilliseconds;
        }
    }

    internal bool ChangeMonitorPreferences(bool bits, int interval)
    {
        interval = MonitorPreferences.NormalizeInterval(interval);
        var previous = (_settings.SpeedInBits, _settings.RefreshMilliseconds);
        if (previous == (bits, interval)) { ApplyMonitorLanguage(); return true; }
        (_settings.SpeedInBits, _settings.RefreshMilliseconds) = (bits, interval);
        var result = _saveSettings(_settings);
        if (!result.Success)
        {
            (_settings.SpeedInBits, _settings.RefreshMilliseconds) = previous;
            ApplyMonitorLanguage();
            ShowSettingsError(result.Error);
            return false;
        }
        if (_timer != null) _timer.Interval = TimeSpan.FromMilliseconds(interval);
        ApplyMonitorLanguage();
        QueueWidgetRender();
        return true;
    }
}
