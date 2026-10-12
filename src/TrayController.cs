using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace TrafficSense;

internal sealed class TrayController : IDisposable
{
    private readonly MainWindow _window;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _showWidget;
    private readonly Forms.ToolStripMenuItem _positionLock;
    private readonly Forms.ToolStripMenuItem _languageMenu;
    private readonly Forms.ToolStripMenuItem _english;
    private readonly Forms.ToolStripMenuItem _bangla;
    private readonly Forms.ToolStripMenuItem _exit;
    private readonly Forms.ToolStripMenuItem _about;
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
        _languageMenu = new Forms.ToolStripMenuItem { Tag = "language" };
        _english = new Forms.ToolStripMenuItem("English", null, (_, _) => _window.ChangeLanguage("en"));
        _bangla = new Forms.ToolStripMenuItem("বাংলা", null, (_, _) => _window.ChangeLanguage("bn"));
        _languageMenu.DropDownItems.AddRange([_english, _bangla]);
        _menu.Items.Add(_languageMenu);
        _about = new Forms.ToolStripMenuItem("", null, (_, _) => _window.ShowAbout()) { Tag = "about" };
        _menu.Items.Add(_about);
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
        _positionLock.Checked = _window.PositionLocked;
        _showWidget.Checked = _window.IsVisible;
        ApplyLanguage();
        _notifyIcon.Visible = true;
    }

    private void LanguageChanged(object? sender, EventArgs e) => ApplyLanguage();
    private void PositionLockChanged(object? sender, EventArgs e) => _positionLock.Checked = _window.PositionLocked;

    private void ApplyLanguage()
    {
        _showWidget.Text = _window.Localization[TextKey.ShowWidget];
        _positionLock.Text = _window.Localization[TextKey.LockPosition];
        _languageMenu.Text = _window.Localization[TextKey.Language];
        _exit.Text = _window.Localization[TextKey.Exit];
        _about.Text = _window.Localization[TextKey.About];
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
        _notifyIcon.DoubleClick -= ShowWidget;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var download = new Pen(Color.FromArgb(76, 175, 80), 4) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var upload = new Pen(Color.FromArgb(33, 150, 243), 4) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawLine(download, 9, 5, 9, 26);
            graphics.DrawLines(download, new Point[] { new(3, 20), new(9, 26), new(15, 20) });
            graphics.DrawLine(upload, 23, 27, 23, 6);
            graphics.DrawLines(upload, new Point[] { new(17, 12), new(23, 6), new(29, 12) });
        }
        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally { DestroyIcon(handle); }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
