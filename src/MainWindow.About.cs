using System.Windows;

namespace TrafficSense;

public partial class MainWindow
{
    private AboutWindow? _aboutWindow;

    private void MenuAbout_Click(object sender, RoutedEventArgs e) => ShowAbout();

    internal void ShowAbout()
    {
        if (_fatalShutdown || Volatile.Read(ref _monitoringClosed) != 0) return;
        if (_aboutWindow == null)
        {
            _aboutWindow = new AboutWindow(Localization, _diagnostics ?? DiagnosticLog.Default,
                _reportError, _launchLogFolder, _updates, _openReleasePage);
            _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        }
        // Keep this window independent of WPF owner visibility: opening About
        // from the tray must work while the widget stays hidden.
        if (_aboutWindow.WindowState == WindowState.Minimized)
            _aboutWindow.WindowState = WindowState.Normal;
        _aboutWindow.Show();
        if (_aboutWindow.ShowActivated) _aboutWindow.Activate();
    }

    private void CloseAbout() => _aboutWindow?.Close();
}
