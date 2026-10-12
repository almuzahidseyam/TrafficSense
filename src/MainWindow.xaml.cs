using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace TrafficSense
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? _timer;
        private readonly NetworkSpeedCalculator _speedCalculator = new();
        private readonly NetworkAdapterSelection _adapterSelection = new();
        private readonly Stopwatch _samplingClock = Stopwatch.StartNew();
        private AppSettings _settings;
        private readonly Func<AppSettings, SettingsSaveResult> _saveSettings;
        private readonly Action<string>? _reportError;
        private NetworkAdapterInfo? _monitoredAdapter;
        private NetworkSpeed _latestSpeed;
        private StartupStatus _startupStatus = new(false);
        internal UiLanguage Localization { get; }
        private HwndSource? _windowSource;
        private bool _placementReady;
        private bool _placementQueued;
        private bool _renderQueued;

        public MainWindow() : this(SettingsManager.Load(), SettingsManager.Save) { }

        internal MainWindow(AppSettings settings, Func<AppSettings, SettingsSaveResult> saveSettings,
            Action<string>? reportError = null)
        {
            _settings = settings;
            _saveSettings = saveSettings;
            _reportError = reportError;
            _settings.Language = UiLanguage.Normalize(_settings.Language);
            Localization = new UiLanguage(_settings.Language);
            InitializeComponent();
            Localization.Changed += Localization_Changed;
            ApplyLanguage();
            
            this.Loaded += (s, e) => 
            {
                if (!_placementReady)
                    NativeWindowPlacement.Restore(this, _settings);
                else
                    NativeWindowPlacement.EnsureVisible(this);
                _placementReady = true;
                ApplyAutoStartStatus(SettingsManager.GetAutoStart());
                ApplyTheme();
            };

            IsVisibleChanged += (_, _) =>
            {
                if (!IsVisible || _renderQueued) return;
                _renderQueued = true;
                // A second launch or tray command can show and hide again in
                // one dispatcher turn. Do not mutate a view that will never
                // render: transparent-window resources otherwise accumulate.
                Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                {
                    _renderQueued = false;
                    if (IsVisible)
                        RenderNetworkSpeed();
                }));
            };

            SourceInitialized += (_, _) =>
            {
                _windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                _windowSource?.AddHook(WindowMessage);
            };
            SizeChanged += (_, _) => QueueKeepVisible();

            InitializeNetworkMonitor();
            Closed += (_, _) =>
            {
                Localization.Changed -= Localization_Changed;
                _placementReady = false;
                if (_windowSource is { IsDisposed: false })
                    _windowSource.RemoveHook(WindowMessage);
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Tick -= Timer_Tick;
                }
            };
        }

        private IntPtr WindowMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Recheck after display, work-area/taskbar, or DPI changes. Let WPF
            // process the message first so the new widget dimensions are available.
            if (message is 0x007E or 0x001A or 0x02E0)
                QueueKeepVisible();
            return IntPtr.Zero;
        }

        private void QueueKeepVisible()
        {
            if (!_placementReady || _placementQueued)
                return;
            _placementQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                _placementQueued = false;
                if (_placementReady)
                    NativeWindowPlacement.EnsureVisible(this);
            }));
        }

        private void InitializeNetworkMonitor()
        {
            RefreshNetworkSpeed();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            RefreshNetworkSpeed();
        }

        private void RefreshNetworkSpeed()
        {
            var adapters = NetworkSnapshotReader.Read();
            var adapter = _adapterSelection.Select(adapters, _settings.SelectedAdapterId);
            NetworkAdapterSnapshot[] snapshots = adapter?.Snapshot is { } snapshot ? [snapshot] : [];
            _latestSpeed = _speedCalculator.Update(snapshots, _samplingClock.Elapsed);
            _monitoredAdapter = adapter;
            // Hidden transparent WPF windows do not need text/layout updates.
            // Keep sampling so showing the widget uses the latest interval.
            if (IsVisible)
                RenderNetworkSpeed();
        }

        private void RenderNetworkSpeed()
        {
            TxtDownload.Text = SpeedFormatter.Format(_latestSpeed.DownloadBytesPerSecond);
            TxtUpload.Text = SpeedFormatter.Format(_latestSpeed.UploadBytesPerSecond);
            ApplyNetworkTooltip();
        }

        private void ApplyNetworkTooltip()
        {
            string status = _monitoredAdapter != null
                ? $"{(_settings.SelectedAdapterId == null ? Localization[TextKey.AutoPrefix] : "")}{_monitoredAdapter.Name}\n{_monitoredAdapter.Description}"
                : _settings.SelectedAdapterId == null
                    ? Localization[TextKey.NoConnectedAdapter]
                    : Localization[TextKey.WaitingForAdapter];
            ((Border)Content).ToolTip = status + "\n" + Localization[TextKey.ChooseAdapterHint];
        }

        private void MenuNetworkAdapter_Opened(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, MenuNetworkAdapter))
                return;

            BuildAdapterMenu();
        }

        private void BuildAdapterMenu()
        {
            var adapters = NetworkSnapshotReader.Read();
            MenuNetworkAdapter.Items.Clear();
            AddAdapterMenuItem(Localization[TextKey.AutoConnection], null, Localization[TextKey.AutoDescription]);
            MenuNetworkAdapter.Items.Add(new Separator());
            foreach (var adapter in adapters.OrderBy(adapter => adapter.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                AddAdapterMenuItem(adapter.Name + (adapter.CanMonitor ? "" : Localization[TextKey.UnavailableSuffix]),
                    adapter.Id, adapter.Description);
            }
            if (_settings.SelectedAdapterId != null && adapters.All(adapter => adapter.Id != _settings.SelectedAdapterId))
            {
                var missing = AddAdapterMenuItem(Localization[TextKey.MissingAdapter], _settings.SelectedAdapterId,
                    Localization[TextKey.ReconnectDescription]);
                missing.IsEnabled = false;
            }
        }

        private MenuItem AddAdapterMenuItem(string name, string? id, string description)
        {
            var item = new MenuItem
            {
                // AccessText treats underscores in adapter names as mnemonics.
                Header = name.Replace("_", "__"), Tag = id, ToolTip = description,
                IsCheckable = true, IsChecked = _settings.SelectedAdapterId == id
            };
            item.Click += MenuAdapter_Click;
            MenuNetworkAdapter.Items.Add(item);
            return item;
        }

        private void MenuAdapter_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            string? previousId = _settings.SelectedAdapterId;
            _settings.SelectedAdapterId = item.Tag as string;
            var result = _saveSettings(_settings);
            if (!result.Success)
            {
                _settings.SelectedAdapterId = previousId;
                ShowSettingsError(result.Error);
            }
            foreach (var option in MenuNetworkAdapter.Items.OfType<MenuItem>())
                option.IsChecked = (option.Tag as string) == _settings.SelectedAdapterId;

            // Even switching Auto/manual on the same ID starts a fresh interval.
            _speedCalculator.Update([], _samplingClock.Elapsed);
            RefreshNetworkSpeed();
            e.Handled = true;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
                NativeWindowPlacement.EnsureVisible(this);
            }
        }

        internal void ShowWidget()
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Show();
            NativeWindowPlacement.EnsureVisible(this);
            Activate();
        }

        private void MenuHide_Click(object sender, RoutedEventArgs e) => Hide();

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            NativeWindowPlacement.Capture(this, _settings);
            var result = _saveSettings(_settings);
            if (!result.Success)
                ShowSettingsError(result.Error);
        }

        private void MenuAutoStart_Click(object sender, RoutedEventArgs e)
        {
            var status = SettingsManager.SetAutoStart(MenuAutoStart.IsChecked);
            ApplyAutoStartStatus(status);
            if (status.Error != null)
                ShowSettingsError(status.Error, StartupErrorKey(status.Failure));
        }

        private void ApplyAutoStartStatus(StartupStatus status)
        {
            _startupStatus = status;
            MenuAutoStart.IsChecked = status.Enabled ?? false;
            MenuAutoStart.IsEnabled = status.Enabled.HasValue;
            MenuAutoStart.ToolTip = status.Error == null ? null
                : ErrorMessage(status.Error, StartupErrorKey(status.Failure));
        }

        private static TextKey StartupErrorKey(StartupError? failure) => failure switch
        {
            StartupError.ReadFailed => TextKey.StartupReadFailed,
            StartupError.ChangeFailed => TextKey.StartupChangeFailed,
            StartupError.NotApplied => TextKey.StartupNotApplied,
            _ => TextKey.SettingsSaveFailed
        };

        private string ErrorMessage(string? details, TextKey headline) => Localization[headline]
            + (string.IsNullOrWhiteSpace(details) ? "" : "\n\n" + details);

        private void ShowSettingsError(string? details, TextKey headline = TextKey.SettingsSaveFailed)
        {
            string message = ErrorMessage(details, headline);
            if (_reportError != null) _reportError(message);
            else MessageBox.Show(this, message, Localization[TextKey.ErrorTitle],
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void MenuTheme_Click(object sender, RoutedEventArgs e)
        {
            string previousTheme = _settings.Theme;
            _settings.Theme = previousTheme == "Dark" ? "Light" : "Dark";
            var result = _saveSettings(_settings);
            if (!result.Success)
            {
                _settings.Theme = previousTheme;
                ShowSettingsError(result.Error);
            }
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            var border = (System.Windows.Controls.Border)this.Content;
            if (_settings.Theme == "Light")
            {
                border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 255, 255, 255));
                TxtDownload.Foreground = System.Windows.Media.Brushes.Black;
                TxtUpload.Foreground = System.Windows.Media.Brushes.Black;
            }
            else
            {
                border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 0, 0, 0));
                TxtDownload.Foreground = System.Windows.Media.Brushes.White;
                TxtUpload.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
