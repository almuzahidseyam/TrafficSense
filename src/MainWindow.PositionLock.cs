using System.Windows;
using System.Windows.Input;

namespace TrafficSense;

public partial class MainWindow
{
    internal bool PositionLocked => _settings.PositionLocked;
    internal event EventHandler? PositionLockChanged;

    internal bool ResetPosition()
    {
        var previous = (_settings.WindowLeft, _settings.WindowTop, _settings.ScreenLeft, _settings.ScreenTop);
        NativeWindowPlacement.Restore(this, new AppSettings());
        NativeWindowPlacement.Capture(this, _settings);
        var result = _saveSettings(_settings);
        if (result.Success) { _restorePlacementPending = false; return true; }
        (_settings.WindowLeft, _settings.WindowTop, _settings.ScreenLeft, _settings.ScreenTop) = previous;
        NativeWindowPlacement.Restore(this, _settings);
        ShowSettingsError(result.Error);
        return false;
    }

    private void MenuResetPosition_Click(object sender, RoutedEventArgs e) => ResetPosition();

    internal bool ChangePositionLock(bool locked)
    {
        if (PositionLocked == locked)
        {
            MenuPositionLock.IsChecked = PositionLocked;
            return true;
        }

        var previous = (_settings.PositionLocked, _settings.WindowLeft, _settings.WindowTop,
            _settings.ScreenLeft, _settings.ScreenTop);
        _settings.PositionLocked = locked;
        // Remember the position being locked immediately, including after a drag.
        if (locked && _placementReady && !_restorePlacementPending) NativeWindowPlacement.Capture(this, _settings);
        var result = _saveSettings(_settings);
        if (!result.Success)
        {
            (_settings.PositionLocked, _settings.WindowLeft, _settings.WindowTop,
                _settings.ScreenLeft, _settings.ScreenTop) = previous;
            MenuPositionLock.IsChecked = PositionLocked;
            ShowSettingsError(result.Error);
            return false;
        }

        MenuPositionLock.IsChecked = PositionLocked;
        PositionLockChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void MenuPositionLock_Click(object sender, RoutedEventArgs e)
    {
        ChangePositionLock(MenuPositionLock.IsChecked);
        e.Handled = true;
    }

    internal bool TryMoveByDrag(MouseButtonState buttonState)
    {
        if (buttonState != MouseButtonState.Pressed || PositionLocked || _fatalShutdown) return false;
        _dragMove();
        // Automatic monitor/work-area recovery remains active even when locked.
        NativeWindowPlacement.EnsureVisible(this);
        return true;
    }
}
