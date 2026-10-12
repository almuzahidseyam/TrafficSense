using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
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
        private readonly bool _backgroundSampling;
        private NetworkSamplingService? _sampler;
        private SampleQuality _sampleQuality = SampleQuality.Sampling;
        private readonly Func<IReadOnlyCollection<NetworkAdapterInfo>> _readAdapters;
        private readonly Func<TimeSpan> _sampleTimestamp;
        private int _baselineResetPending;
        private int _monitoringClosed;
        private AppSettings _settings;
        private readonly Func<AppSettings, SettingsSaveResult> _saveSettings;
        private readonly Action<string>? _reportError;
        private readonly DiagnosticLog? _diagnostics;
        private readonly Action<string>? _launchLogFolder;
        private readonly UpdateChecker? _updates;
        private readonly Action<Uri>? _openReleasePage;
        private readonly Action _dragMove;
        private readonly GlobalShortcut _shortcut;
        private bool _fatalShutdown;
        private NetworkAdapterInfo? _monitoredAdapter;
        private NetworkSpeed _latestSpeed;
        private StartupStatus _startupStatus = new(false);
        internal UiLanguage Localization { get; }
        private HwndSource? _windowSource;
        private bool _placementReady;
        private bool _placementQueued;
        private bool _renderQueued;

        public MainWindow() : this(SettingsManager.Load(), SettingsManager.Save, diagnostics: DiagnosticLog.Default,
            usageStore: new UsageStore(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TrafficSense", "usage.json"), DiagnosticLog.Default)) { }

        internal MainWindow(AppSettings settings, Func<AppSettings, SettingsSaveResult> saveSettings,
            Action<string>? reportError = null,
            Func<IReadOnlyCollection<NetworkAdapterInfo>>? readAdapters = null,
            Func<TimeSpan>? sampleTimestamp = null, DiagnosticLog? diagnostics = null,
            Action<string>? launchLogFolder = null, UpdateChecker? updates = null, Action<Uri>? openReleasePage = null,
            Action? dragMove = null, UsageStore? usageStore = null, Func<DateTimeOffset>? localTime = null,
            GlobalShortcut? shortcut = null, Func<SystemMetric, SystemMetrics>? readSystemMetrics = null, IAppBarShell? appBarShell = null,
            Action<UsageAlertNotification>? usageNotificationSink = null)
        {
            _sampleTimestamp = sampleTimestamp ?? (() => _samplingClock.Elapsed);
            _readAdapters = readAdapters ?? (() => NetworkSnapshotReader.Read(_sampleTimestamp));
            _readSystemMetrics = readSystemMetrics;
            _appBarShell = appBarShell;
            _usageNotificationSink = usageNotificationSink;
            _backgroundSampling = readAdapters == null && sampleTimestamp == null;
            _settings = settings;
            _saveSettings = saveSettings;
            _reportError = reportError;
            _diagnostics = diagnostics;
            _launchLogFolder = launchLogFolder;
            _updates = updates;
            _openReleasePage = openReleasePage;
            _dragMove = dragMove ?? DragMove;
            _shortcut = shortcut ?? new GlobalShortcut();
            _usageStore = usageStore;
            _usage = usageStore?.Load() ?? new();
            _localTime = localTime ?? (() => DateTimeOffset.Now);
            _settings.Language = UiLanguage.Normalize(_settings.Language);
            _settings.WidgetFontSize = WidgetAppearance.NormalizeFontSize(_settings.WidgetFontSize);
            _settings.BackgroundOpacity = WidgetAppearance.NormalizeBackgroundOpacity(_settings.BackgroundOpacity);
            _settings.RefreshMilliseconds = MonitorPreferences.NormalizeInterval(_settings.RefreshMilliseconds);
            Localization = new UiLanguage(_settings.Language);
            InitializeComponent();
            InitializeAppearanceMenus();
            InitializeMonitorMenus();
            InitializeSystemMetrics();
            InitializeCustomization();
            Localization.Changed += Localization_Changed;
            ApplyLanguage();
            ApplyPendingAppearance();
            
            this.Loaded += (s, e) => 
            {
                if (!_placementReady)
                    NativeWindowPlacement.Restore(this, _settings);
                else
                    NativeWindowPlacement.EnsureVisible(this);
                _placementReady = true;
                ApplyAutoStartStatus(SettingsManager.GetAutoStart());
                ConfigureTaskbarPanel();
            };

            IsVisibleChanged += (_, _) => QueueWidgetRender();

            SourceInitialized += (_, _) =>
            {
                _windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                _windowSource?.AddHook(WindowMessage);
                _shortcut.Attach(new WindowInteropHelper(this).Handle);
                if (_settings.GlobalShortcutEnabled && !_shortcut.SetEnabled(true))
                    _diagnostics?.Write("Shortcut", "Ctrl+Alt+T is unavailable.");
                ApplyKeyboardLanguage();
            };
            SizeChanged += (_, _) => QueueKeepVisible();
            LocationChanged += (_, _) => _taskbarPanel?.Reposition();

            NetworkChange.NetworkAddressChanged += NetworkChanged;
            NetworkChange.NetworkAvailabilityChanged += NetworkChanged;
            InitializeNetworkMonitor();
            Closed += (_, _) =>
            {
                StopMonitoring();
                _shortcut.Dispose();
                CloseAbout();
                CloseHistory();
                FlushUsage();
                Localization.Changed -= Localization_Changed;
                _placementReady = false;
                if (_windowSource is { IsDisposed: false })
                    _windowSource.RemoveHook(WindowMessage);
            };
        }

        private void QueueWidgetRender()
        {
            if (!IsVisible || _renderQueued || _fatalShutdown) return;
            _renderQueued = true;
            // Coalesce show/hide and appearance changes into a visible frame.
            // Resizing a hidden transparent HWND can retain drawing resources.
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                _renderQueued = false;
                if (IsVisible && !_fatalShutdown)
                {
                    ApplyPendingAppearance();
                    ApplyRestoredPosition();
                    RenderNetworkSpeed();
                    RefreshSystemMetrics();
                }
            }));
        }

        private void StopMonitoring()
        {
            if (Interlocked.Exchange(ref _monitoringClosed, 1) != 0) return;
            NetworkChange.NetworkAddressChanged -= NetworkChanged;
            NetworkChange.NetworkAvailabilityChanged -= NetworkChanged;
            _sampler?.Dispose();
            _systemService?.Dispose();
            _systemWindow?.Close();
            _taskbarPanel?.ReleaseAndClose(); _taskbarPanel = null;
            if (_sampler != null) MergeSampledUsage(_sampler.Consume());
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
            }
        }

        internal void PrepareForFatalShutdown()
        {
            _fatalShutdown = true;
            StopMonitoring();
            _shortcut.Dispose();
            CloseAbout();
            CloseHistory();
            IsEnabled = false;
        }

        private IntPtr WindowMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == 0x0312 && wParam.ToInt64() == GlobalShortcut.Id && _shortcut.Enabled && !_fatalShutdown)
            {
                if (IsVisible) Hide(); else ShowWidget();
                handled = true;
                return IntPtr.Zero;
            }
            const int WmPowerBroadcast = 0x0218;
            const int PbtApmSuspend = 0x0004;
            const int PbtApmResumeSuspend = 0x0007;
            const int PbtApmResumeAutomatic = 0x0012;
            if (message == WmPowerBroadcast && wParam.ToInt64() is
                PbtApmSuspend or PbtApmResumeSuspend or PbtApmResumeAutomatic)
            {
                // This hook runs on the UI thread. Clear stale speed immediately;
                // the next timer sample establishes the new counter baseline.
                _speedCalculator.Reset();
                _sampler?.Reset();
                _latestSpeed = default;
                _sampleQuality = SampleQuality.Sampling;
                if (IsVisible) RenderNetworkSpeed();
            }
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
            if (_backgroundSampling)
                _sampler = new NetworkSamplingService(_readAdapters, _sampleTimestamp, _localTime,
                    _settings.SelectedAdapterId, _settings.RefreshMilliseconds, _diagnostics);
            RefreshNetworkSpeed();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(_settings.RefreshMilliseconds);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            RefreshNetworkSpeed();
            RefreshSystemMetrics();
            RefreshTaskbarPanel();
        }

        private void NetworkChanged(object? sender, EventArgs e)
        {
            // NetworkChange callbacks can run on a worker thread. Coalesce them
            // without touching WPF or queueing UI work after the window closes.
            if (Volatile.Read(ref _monitoringClosed) == 0)
            {
                _sampler?.Reset();
                Interlocked.Exchange(ref _baselineResetPending, 1);
            }
        }

        private void RefreshNetworkSpeed()
        {
            if (_sampler != null)
            {
                Interlocked.Exchange(ref _baselineResetPending, 0);
                var delivery = _sampler.Consume();
                _latestSpeed = delivery.Speed; _monitoredAdapter = delivery.Adapter; _sampleQuality = delivery.Quality;
                if (_sampleTimestamp() - delivery.Timestamp > TimeSpan.FromSeconds(5))
                { _latestSpeed = default; _sampleQuality = SampleQuality.Delayed; }
                MergeSampledUsage(delivery);
                RecordHistory(delivery.Timestamp);
                if (IsVisible) RenderNetworkSpeed();
                return;
            }
            var adapters = _readAdapters();
            if (Interlocked.Exchange(ref _baselineResetPending, 0) != 0)
                _speedCalculator.Reset();
            var adapter = _adapterSelection.Select(adapters, _settings.SelectedAdapterId);
            NetworkAdapterSnapshot[] snapshots = adapter?.Snapshot is { } snapshot ? [snapshot] : [];
            var timestamp = _sampleTimestamp();
            _latestSpeed = _speedCalculator.Update(snapshots, timestamp);
            _sampleQuality = _speedCalculator.Quality;
            _monitoredAdapter = adapter;
            RecordTraffic(timestamp);
            // Hidden transparent WPF windows do not need text/layout updates.
            // Keep sampling so showing the widget uses the latest interval.
            if (IsVisible)
                RenderNetworkSpeed();
        }

        private void RenderNetworkSpeed()
        {
            if (_fatalShutdown) return;
            TxtDownload.Text = SpeedFormatter.Format(_latestSpeed.DownloadBytesPerSecond, _settings.SpeedInBits);
            TxtUpload.Text = SpeedFormatter.Format(_latestSpeed.UploadBytesPerSecond, _settings.SpeedInBits);
            TxtSampleState.Text = _sampleQuality == SampleQuality.Fresh ? "" : Localization[_sampleQuality == SampleQuality.Unavailable ? TextKey.SampleUnavailableShort :
                _sampleQuality == SampleQuality.Delayed ? TextKey.SampleDelayedShort : TextKey.SamplingShort];
            TxtSampleState.Visibility = TxtSampleState.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            System.Windows.Automation.AutomationProperties.SetName(TxtSampleState, TxtSampleState.Text);
            ApplyNetworkTooltip();
        }

        private void ApplyNetworkTooltip()
        {
            string status = _monitoredAdapter != null
                ? $"{(_settings.SelectedAdapterId == null ? Localization[TextKey.AutoPrefix] : "")}{_monitoredAdapter.Name}\n{_monitoredAdapter.Description}"
                : _settings.SelectedAdapterId == null
                    ? Localization[TextKey.NoConnectedAdapter]
                    : Localization[TextKey.WaitingForAdapter];
            if (_monitoredAdapter != null)
                status += "\n" + _monitoredAdapter.Addresses + "\nLink: " + SpeedFormatter.Format(_monitoredAdapter.LinkBitsPerSecond / 8d, true);
            if (_sampleQuality != SampleQuality.Fresh)
                status += "\n" + Localization[_sampleQuality == SampleQuality.Unavailable ? TextKey.SampleUnavailable :
                    _sampleQuality == SampleQuality.Delayed ? TextKey.SampleDelayed : TextKey.Sampling];
            ((Border)Content).ToolTip = status + "\n" + Localization[TextKey.ChooseAdapterHint] + "\n" + Localization[TextKey.MeasurementHint];
        }

        private void MenuNetworkAdapter_Opened(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, MenuNetworkAdapter))
                return;

            BuildAdapterMenu();
        }

        private void BuildAdapterMenu()
        {
            var adapters = _readAdapters();
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
            _speedCalculator.Reset();
            _sampler?.Configure(_settings.SelectedAdapterId, _settings.RefreshMilliseconds, reset: true);
            RefreshNetworkSpeed();
            e.Handled = true;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) Focus();
            TryMoveByDrag(e.ButtonState);
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
            // Retain the last successfully saved preferences after a fatal error.
            if (_fatalShutdown) return;
            if (!_restorePlacementPending) NativeWindowPlacement.Capture(this, _settings);
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
            if (status.Failure == StartupError.ReadFailed)
                _diagnostics?.Write("StartupRead", status.Error);
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
            _diagnostics?.Write(headline.ToString(), details);
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
                border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)_settings.BackgroundOpacity, 255, 255, 255));
                TxtDownload.Foreground = System.Windows.Media.Brushes.Black;
                TxtUpload.Foreground = System.Windows.Media.Brushes.Black;
                TxtMetrics.Foreground = System.Windows.Media.Brushes.Black;
            }
            else
            {
                border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)_settings.BackgroundOpacity, 0, 0, 0));
                TxtDownload.Foreground = System.Windows.Media.Brushes.White;
                TxtUpload.Foreground = System.Windows.Media.Brushes.White;
                TxtMetrics.Foreground = System.Windows.Media.Brushes.White;
            }
            ApplyCustomStyle();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
