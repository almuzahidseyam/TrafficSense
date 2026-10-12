using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace TrafficSense;

public partial class MainWindow
{
    private bool _restorePlacementPending;

    internal bool ExportSettings(string path)
    {
        var snapshot = SettingsStore.Normalize(_settings);
        if (_placementReady && !_restorePlacementPending) NativeWindowPlacement.Capture(this, snapshot);
        var result = SettingsBackup.Export(path, snapshot);
        if (!result.Success) ShowSettingsError(result.Error, TextKey.BackupFailed);
        return result.Success;
    }

    internal bool RestoreSettings(string path)
    {
        if (_fatalShutdown) return false;
        AppSettings candidate;
        try { candidate = SettingsBackup.Read(path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or
            InvalidOperationException or ArgumentException or NotSupportedException)
        { ShowSettingsError(error.Message, TextKey.RestoreFailed); return false; }

        // Adapter IDs belong to a machine. A missing adapter from a
        // different PC should not leave a restored widget permanently waiting.
        if (candidate.SelectedAdapterId != null && !_readAdapters().Any(adapter => adapter.Id == candidate.SelectedAdapterId))
            candidate.SelectedAdapterId = null;
        bool shortcutWasActive = _shortcut.Enabled;
        if (candidate.GlobalShortcutEnabled && !_shortcut.SetEnabled(true))
        { ShowSettingsError(null, TextKey.ShortcutUnavailable); return false; }
        var result = _saveSettings(candidate);
        if (!result.Success)
        {
            _shortcut.SetEnabled(shortcutWasActive);
            ShowSettingsError(result.Error);
            return false;
        }

        bool adapterChanged = _settings.SelectedAdapterId != candidate.SelectedAdapterId;
        _settings = candidate;
        _shortcut.SetEnabled(candidate.GlobalShortcutEnabled);
        if (_timer != null) _timer.Interval = TimeSpan.FromMilliseconds(candidate.RefreshMilliseconds);
        _sampler?.Configure(candidate.SelectedAdapterId, candidate.RefreshMilliseconds, adapterChanged);
        ConfigureSystemMetrics();
        _appearancePending = true;
        _restorePlacementPending = true;
        if (adapterChanged) { _speedCalculator.Reset(); _latestSpeed = default; _monitoredAdapter = null; }
        if (Localization.Code == candidate.Language) ApplyLanguage();
        else Localization.Select(candidate.Language);
        PositionLockChanged?.Invoke(this, EventArgs.Empty);
        _historyWindow?.Refresh();
        QueueWidgetRender();
        return true;
    }

    private void ApplyRestoredPosition()
    {
        if (!_restorePlacementPending || !_placementReady) return;
        NativeWindowPlacement.Restore(this, _settings);
        _restorePlacementPending = false;
    }

    private void MenuBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = Localization[TextKey.BackupSettings], Filter = "TrafficSense settings (*.json)|*.json",
            FileName = "TrafficSense-settings.json", DefaultExt = ".json", AddExtension = true, OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) == true) ExportSettings(dialog.FileName);
    }

    private void MenuRestore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Localization[TextKey.RestoreSettings], Filter = "TrafficSense settings (*.json)|*.json",
            CheckFileExists = true, Multiselect = false
        };
        if (dialog.ShowDialog(this) == true) RestoreSettings(dialog.FileName);
    }
}
