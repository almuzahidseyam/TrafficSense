using System.Drawing;
using Forms = System.Windows.Forms;

namespace TrafficSense;

internal sealed class TrayController : IDisposable
{
    private readonly MainWindow _window;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _showWidget;
    private readonly Forms.ToolStripMenuItem _positionLock;
    private readonly Forms.ToolStripMenuItem _resetPosition;
    private readonly Forms.ToolStripMenuItem _languageMenu;
    private readonly Forms.ToolStripMenuItem _english;
    private readonly Forms.ToolStripMenuItem _bangla;
    private readonly Forms.ToolStripMenuItem _exit;
    private readonly Forms.ToolStripMenuItem _about;
    private readonly Forms.ToolStripMenuItem _history;
    private readonly Icon _icon;
    private bool _disposed;

    public TrayController(MainWindow window)
    {
        _window = window;
        _icon = CreateIcon();
        _menu = new Forms.ContextMenuStrip();
        _showWidget = new Forms.ToolStripMenuItem("", null, ToggleWidget) { Tag = "show" };
        _menu.Items.Add(_showWidget);
        _positionLock = new Forms.ToolStripMenuItem("", null,
            (_, _) => _window.ChangePositionLock(!_window.PositionLocked)) { Tag = "position-lock" };
        _menu.Items.Add(_positionLock);
        _resetPosition = new Forms.ToolStripMenuItem("", null, (_, _) => _window.ResetPosition()) { Tag = "reset-position" };
        _menu.Items.Add(_resetPosition);
        _languageMenu = new Forms.ToolStripMenuItem { Tag = "language" };
        _english = new Forms.ToolStripMenuItem("English", null, (_, _) => _window.ChangeLanguage("en"));
        _bangla = new Forms.ToolStripMenuItem("বাংলা", null, (_, _) => _window.ChangeLanguage("bn"));
        _languageMenu.DropDownItems.AddRange([_english, _bangla]);
        _menu.Items.Add(_languageMenu);
        _about = new Forms.ToolStripMenuItem("", null, (_, _) => _window.ShowAbout()) { Tag = "about" };
        _menu.Items.Add(_about);
        _history = new Forms.ToolStripMenuItem("", null, (_, _) => _window.ShowHistory()) { Tag = "history" };
        _menu.Items.Add(_history);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _exit = new Forms.ToolStripMenuItem("", null, Exit) { Tag = "exit" };
        _menu.Items.Add(_exit);
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "TrafficSense",
            Icon = _icon,
            ContextMenuStrip = _menu
        };
        _notifyIcon.DoubleClick += ShowWidget;
        _window.IsVisibleChanged += WindowVisibilityChanged;
        _window.Localization.Changed += LanguageChanged;
        _window.PositionLockChanged += PositionLockChanged;
        _window.UsageAlert += ShowUsageAlert;
        _positionLock.Checked = _window.PositionLocked;
        _showWidget.Checked = _window.IsVisible;
        ApplyLanguage();
        _notifyIcon.Visible = true;
    }

    private void LanguageChanged(object? sender, EventArgs e) => ApplyLanguage();
    private void PositionLockChanged(object? sender, EventArgs e) => _positionLock.Checked = _window.PositionLocked;
    private void ShowUsageAlert(UsageAlertNotification notification)
    {
        if (!_disposed) _notifyIcon.ShowBalloonTip(5000, notification.Title, notification.Message, Forms.ToolTipIcon.Warning);
    }

    private void ApplyLanguage()
    {
        _showWidget.Text = _window.Localization[TextKey.ShowWidget];
        _positionLock.Text = _window.Localization[TextKey.LockPosition];
        _resetPosition.Text = _window.Localization[TextKey.ResetPosition];
        _languageMenu.Text = _window.Localization[TextKey.Language];
        _exit.Text = _window.Localization[TextKey.Exit];
        _about.Text = _window.Localization[TextKey.About];
        _history.Text = _window.Localization[TextKey.History];
        _english.Checked = _window.Localization.Code == "en";
        _bangla.Checked = _window.Localization.Code == "bn";
        _notifyIcon.Text = _window.Localization[TextKey.TrayHint];
    }

    private void ShowWidget(object? sender, EventArgs e) => _window.ShowWidget();

    private void ToggleWidget(object? sender, EventArgs e)
    {
        if (_window.IsVisible) _window.Hide();
        else _window.ShowWidget();
    }

    private void WindowVisibilityChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e) =>
        _showWidget.Checked = _window.IsVisible;

    private void Exit(object? sender, EventArgs e) => _window.Close();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _window.IsVisibleChanged -= WindowVisibilityChanged;
        _window.Localization.Changed -= LanguageChanged;
        _window.PositionLockChanged -= PositionLockChanged;
        _window.UsageAlert -= ShowUsageAlert;
        _notifyIcon.DoubleClick -= ShowWidget;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    private static Icon CreateIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/TrafficSense;component/Assets/TrafficSense.ico"))
            ?? throw new InvalidOperationException("TrafficSense icon resource is missing.");
        using var stream = resource.Stream;
        using var source = new Icon(stream, Forms.SystemInformation.SmallIconSize);
        return (Icon)source.Clone();
    }
}
