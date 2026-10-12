using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace TrafficSense;

internal sealed class TaskbarPanelWindow : Window
{
    internal const uint CallbackMessage = 0x8000 + 87;
    internal static readonly uint ExplorerCreatedMessage = RegisterWindowMessage("TaskbarCreated");
    private readonly IAppBarShell _shell;
    private readonly Func<DockBounds> _monitor;
    private readonly Action _disable;
    private readonly TextBlock _speed = new() { Margin = new(8, 2, 14, 2), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _metrics = new() { Margin = new(0, 2, 14, 2), VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _history = new() { Style = null, Margin = new(4, 2, 4, 2), Padding = new(6, 0, 6, 0) };
    private readonly Button _settings = new() { Style = null, Margin = new(4, 2, 4, 2), Padding = new(6, 0, 6, 0) };
    private readonly Button _close = new() { Style = null, Content = "×", Margin = new(4, 2, 8, 2), MinWidth = 28 };
    private HwndSource? _source;
    private IntPtr _handle;
    private bool _registered, _closing, _queued, _positioning, _top, _fullscreen;
    private int _fontSize = 14;
    private DockBounds? _lastPosition;

    internal bool Registered => _registered;
    internal TaskbarPanelWindow(IAppBarShell shell, Func<DockBounds> monitor, Action history, Action settings, Action disable)
    {
        _shell = shell; _monitor = monitor; _disable = disable;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false;
        Topmost = true; Width = 640; Height = 36; FontSize = 14; FontFamily = new FontFamily("Segoe UI, Nirmala UI");
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(_history); buttons.Children.Add(_settings); buttons.Children.Add(_close);
        var root = new DockPanel(); DockPanel.SetDock(buttons, Dock.Right); root.Children.Add(buttons);
        var values = new StackPanel { Orientation = Orientation.Horizontal }; values.Children.Add(_speed); values.Children.Add(_metrics);
        root.Children.Add(new ScrollViewer { Content = values, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Content = root;
        _history.Click += (_, _) => history(); _settings.Click += (_, _) => settings(); _close.Click += (_, _) => disable();
        foreach (var button in new[] { _history, _settings, _close })
        { button.Background = SystemColors.ControlBrush; button.Foreground = SystemColors.ControlTextBrush; button.BorderBrush = SystemColors.ActiveBorderBrush; }
        Closing += (_, e) => { if (!_closing) { e.Cancel = true; Dispatcher.BeginInvoke(new Action(_disable)); } };
        Closed += (_, _) => Detach();
    }

    internal bool TryShow()
    {
        _handle = new WindowInteropHelper(this).EnsureHandle();
        _source = HwndSource.FromHwnd(_handle); _source?.AddHook(Message);
        _registered = _shell.Register(_handle, CallbackMessage);
        if (!_registered) { Detach(); return false; }
        Position(force: true); Show(); return true;
    }

    internal void Update(string download, string upload, string metrics, UiLanguage language, bool top, WidgetSkin skin, bool light)
    {
        string speed = "↓ " + download + "   ↑ " + upload;
        _metrics.Text = metrics.Replace("\n", "   ");
        Title = language[TextKey.TaskbarPanel];
        _history.Content = language[TextKey.History]; _settings.Content = language[TextKey.PanelSettings];
        _close.ToolTip = language[TextKey.ClosePanel]; AutomationProperties.SetName(_close, language[TextKey.ClosePanel]);
        AutomationProperties.SetName(_speed, speed); AutomationProperties.SetName(_metrics, metrics);
        ToolTip = language[TextKey.PanelHint] + "\n" + language[TextKey.MeasurementHint];
        Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : light ? Brushes.White : Brushes.Black;
        Foreground = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : light ? Brushes.Black : Brushes.White;
        Brush ColorBrush(string color) => SystemParameters.HighContrast ? SystemColors.WindowTextBrush : color == "Auto" ? Foreground : new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        if (!SystemParameters.HighContrast && skin.Background != "Auto") Background = ColorBrush(skin.Background);
        _speed.Inlines.Clear(); _speed.Inlines.Add(new Run("↓ " + download) { Foreground = ColorBrush(skin.Download) });
        _speed.Inlines.Add(new Run("   ↑ " + upload) { Foreground = ColorBrush(skin.Upload) });
        _metrics.Foreground = ColorBrush(skin.Metrics);
        FontFamily = new FontFamily(skin.FontFamily); FontWeight = skin.Bold ? FontWeights.SemiBold : FontWeights.Normal;
        if (_top != top || _fontSize != skin.FontSize) { _top = top; _fontSize = skin.FontSize; FontSize = skin.FontSize; QueuePosition(); }
    }

    internal void Reposition() => QueuePosition();
    private void QueuePosition()
    {
        if (_queued || _closing || !_registered) return;
        _queued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => { _queued = false; if (!_closing && _registered) Position(); }));
    }

    private void Position(bool force = false)
    {
        if (!_registered || _positioning) return;
        _positioning = true;
        try
        {
            var bounds = _monitor();
            int height = Math.Max(36, (int)Math.Ceiling((_fontSize + 36) * GetDpiForWindow(_handle) / 96d));
            var proposed = _top ? bounds with { Bottom = bounds.Top + height } : bounds with { Top = bounds.Bottom - height };
            var approved = _shell.Query(_handle, _top, proposed);
            approved = _top ? approved with { Bottom = approved.Top + height } : approved with { Top = approved.Bottom - height };
            if (!force && approved == _lastPosition) return;
            approved = _shell.Set(_handle, _top, approved);
            _lastPosition = approved; _shell.Move(_handle, approved);
            if (_fullscreen) _shell.FullScreen(_handle, true);
        }
        finally { _positioning = false; }
    }

    private IntPtr Message(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_closing) return IntPtr.Zero;
        if (ExplorerCreatedMessage != 0 && (uint)message == ExplorerCreatedMessage)
        {
            _registered = _shell.Register(_handle, CallbackMessage); _lastPosition = null; QueuePosition();
        }
        else if ((uint)message == CallbackMessage && _registered)
        {
            if (wParam.ToInt64() is 0 or 1 && !_positioning) QueuePosition(); // ABN_STATE/POSCHANGED
            else if (wParam.ToInt64() == 2) { _fullscreen = lParam != IntPtr.Zero; _shell.FullScreen(_handle, _fullscreen); }
            else if (wParam.ToInt64() == 3) _shell.Arrange(_handle, lParam != IntPtr.Zero);
        }
        else if (message is 0x007E or 0x001A or 0x02E0) { _lastPosition = null; QueuePosition(); }
        else if (message == 0x0006 && _registered) _shell.Notify(_handle, 6); // ABM_ACTIVATE
        else if (message == 0x0047 && _registered) _shell.Notify(_handle, 9); // ABM_WINDOWPOSCHANGED
        return IntPtr.Zero;
    }

    internal void ReleaseAndClose() { if (_closing) return; _closing = true; Detach(); Close(); }
    private void Detach()
    {
        if (_registered) { _shell.Unregister(_handle); _registered = false; }
        if (_source is { IsDisposed: false }) _source.RemoveHook(Message);
        _source = null;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
}
