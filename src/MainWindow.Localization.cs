using System.Windows;
using System.Windows.Controls;

namespace TrafficSense;

public partial class MainWindow
{
    private void Localization_Changed(object? sender, EventArgs e) => ApplyLanguage();

    private void ApplyLanguage()
    {
        ApplyNavigationLanguage();
        MenuNetworkAdapter.Header = Localization[TextKey.NetworkAdapter];
        MenuAutoStart.Header = Localization[TextKey.StartWithWindows];
        MenuPositionLock.Header = Localization[TextKey.LockPosition];
        MenuPositionLock.IsChecked = PositionLocked;
        MenuResetPosition.Header = Localization[TextKey.ResetPosition];
        MenuTheme.Header = Localization[TextKey.SwitchTheme];
        MenuLanguage.Header = Localization[TextKey.Language];
        MenuHide.Header = Localization[TextKey.HideWidget];
        MenuAbout.Header = Localization[TextKey.About];
        MenuHistory.Header = Localization[TextKey.History];
        MenuExit.Header = Localization[TextKey.Exit];
        MenuSettingsBackup.Header = Localization[TextKey.SettingsBackup];
        MenuSettingsBackup.ToolTip = Localization[TextKey.BackupHint];
        MenuBackup.Header = Localization[TextKey.BackupSettings];
        MenuRestore.Header = Localization[TextKey.RestoreSettings];
        MenuRestore.ToolTip = Localization[TextKey.BackupHint];
        TxtDownload.ToolTip = Localization[TextKey.DownloadSpeed];
        TxtUpload.ToolTip = Localization[TextKey.UploadSpeed];
        UpdateLanguageChecks();
        ApplyAppearanceLanguage();
        ApplyMonitorLanguage();
        ApplySystemLanguage();
        ApplyPanelLanguage();
        ApplyCustomizationLanguage();
        ApplyKeyboardLanguage();
        ApplyAutoStartStatus(_startupStatus);
        BuildAdapterMenu();
        ApplyNetworkTooltip();
    }

    private void UpdateLanguageChecks()
    {
        MenuEnglish.IsChecked = Localization.Code == "en";
        MenuBangla.IsChecked = Localization.Code == "bn";
    }

    internal bool ChangeLanguage(string code)
    {
        string next = UiLanguage.Normalize(code);
        if (Localization.Code == next)
        {
            UpdateLanguageChecks();
            return true;
        }

        string previous = _settings.Language;
        _settings.Language = next;
        var result = _saveSettings(_settings);
        if (!result.Success)
        {
            _settings.Language = previous;
            UpdateLanguageChecks();
            ShowSettingsError(result.Error);
            return false;
        }

        // Publish only after saving succeeds. The tray and widget share this
        // instance, and a failed save leaves both in the previous language.
        Localization.Select(next);
        return true;
    }

    private void MenuLanguage_Click(object sender, RoutedEventArgs e)
    {
        ChangeLanguage((string)((MenuItem)sender).Tag);
        e.Handled = true;
    }
}
