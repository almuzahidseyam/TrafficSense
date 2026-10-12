using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;

namespace TrafficSense;

public partial class MainWindow
{
    internal bool HandleWidgetKey(Key key, ModifierKeys modifiers)
    {
        if (_fatalShutdown) return false;
        if (modifiers == ModifierKeys.Control)
        {
            if (key == Key.L) { ChangePositionLock(!PositionLocked); return true; }
            if (key == Key.R) { ResetPosition(); return true; }
            if (key == Key.H) { ShowHistory(); return true; }
        }
        if (key == Key.F1 && modifiers == ModifierKeys.None) { ShowAbout(); return true; }
        if (key == Key.Escape && modifiers == ModifierKeys.None) { Hide(); return true; }
        if ((key == Key.F10 && modifiers == ModifierKeys.Shift) || key == Key.Apps)
        {
            var menu = ((Border)Content).ContextMenu;
            menu.PlacementTarget = this; menu.IsOpen = true;
            menu.Dispatcher.BeginInvoke(new Action(() => ((MenuItem)menu.Items[0]).Focus()));
            return true;
        }
        if (!PositionLocked && modifiers is ModifierKeys.None or ModifierKeys.Shift && key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            double distance = modifiers == ModifierKeys.Shift ? 1 : 10;
            if (key == Key.Left) Left -= distance;
            if (key == Key.Right) Left += distance;
            if (key == Key.Up) Top -= distance;
            if (key == Key.Down) Top += distance;
            NativeWindowPlacement.EnsureVisible(this);
            return true;
        }
        return false;
    }

    private void Widget_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        e.Handled = HandleWidgetKey(key, Keyboard.Modifiers);
    }

    private void MenuShortcut_Click(object sender, RoutedEventArgs e) => ChangeGlobalShortcut(MenuShortcut.IsChecked);
    internal bool ChangeGlobalShortcut(bool enabled)
    {
        bool previous = _settings.GlobalShortcutEnabled;
        bool active = _shortcut.Enabled;
        if (previous == enabled && active == enabled) { ApplyKeyboardLanguage(); return true; }
        if (enabled && !_shortcut.SetEnabled(true))
        { ApplyKeyboardLanguage(); ShowSettingsError(null, TextKey.ShortcutUnavailable); return false; }
        _settings.GlobalShortcutEnabled = enabled;
        var result = _saveSettings(_settings);
        if (!result.Success)
        {
            _settings.GlobalShortcutEnabled = previous;
            _shortcut.SetEnabled(active);
            ApplyKeyboardLanguage(); ShowSettingsError(result.Error); return false;
        }
        _shortcut.SetEnabled(enabled);
        ApplyKeyboardLanguage();
        return true;
    }

    private void ApplyKeyboardLanguage()
    {
        MenuShortcut.Header = Localization[TextKey.GlobalShortcut];
        MenuShortcut.IsChecked = _shortcut.Enabled;
        MenuShortcut.ToolTip = Localization[TextKey.ShortcutHelp];
        AutomationProperties.SetName(this, "TrafficSense");
        AutomationProperties.SetHelpText(this, Localization[TextKey.ShortcutHelp]);
        AutomationProperties.SetName(TxtDownload, Localization[TextKey.DownloadSpeed]);
        AutomationProperties.SetName(TxtUpload, Localization[TextKey.UploadSpeed]);
    }
}
