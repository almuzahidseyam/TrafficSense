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
        private HwndSource? _windowSource;
        private bool _placementReady;
        private bool _placementQueued;

        public MainWindow()
        {
            InitializeComponent();
            _settings = SettingsManager.Load();
            
            this.Loaded += (s, e) => 
            {
                NativeWindowPlacement.Restore(this, _settings);
                _placementReady = true;
                ApplyAutoStartStatus(SettingsManager.GetAutoStart());
                ApplyTheme();
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
            var speed = _speedCalculator.Update(snapshots, _samplingClock.Elapsed);
            TxtDownload.Text = SpeedFormatter.Format(speed.DownloadBytesPerSecond);
            TxtUpload.Text = SpeedFormatter.Format(speed.UploadBytesPerSecond);
            string status = adapter != null
                ? $"{(_settings.SelectedAdapterId == null ? "Auto: " : "")}{adapter.Name}\n{adapter.Description}"
                : _settings.SelectedAdapterId == null
                    ? "No connected network adapter"
                    : "Selected adapter disconnected or unavailable. Waiting for it to reconnect.";
            ((Border)Content).ToolTip = status + "\nRight-click to choose a network adapter.";
        }

        private void MenuNetworkAdapter_Opened(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, MenuNetworkAdapter))
                return;

            var adapters = NetworkSnapshotReader.Read();
            MenuNetworkAdapter.Items.Clear();
            AddAdapterMenuItem("Auto (one connection)", null,
                "Monitors one connected adapter automatically. VPN and underlying traffic are never added together.");
            MenuNetworkAdapter.Items.Add(new Separator());
            foreach (var adapter in adapters.OrderBy(adapter => adapter.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                AddAdapterMenuItem(adapter.Name + (adapter.CanMonitor ? "" : " (disconnected/unavailable)"),
                    adapter.Id, adapter.Description);
            }
            if (_settings.SelectedAdapterId != null && adapters.All(adapter => adapter.Id != _settings.SelectedAdapterId))
            {
                var missing = AddAdapterMenuItem("Selected adapter (unavailable)", _settings.SelectedAdapterId,
                    "This adapter will be monitored again when it reconnects. Choose Auto or another adapter to switch.");
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
            var result = SettingsManager.Save(_settings);
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            NativeWindowPlacement.Capture(this, _settings);
            var result = SettingsManager.Save(_settings);
            if (!result.Success)
                ShowSettingsError(result.Error);
        }

        private void MenuAutoStart_Click(object sender, RoutedEventArgs e)
        {
            var status = SettingsManager.SetAutoStart(MenuAutoStart.IsChecked);
            ApplyAutoStartStatus(status);
            if (status.Error != null)
                ShowSettingsError(status.Error);
        }

        private void ApplyAutoStartStatus(StartupStatus status)
        {
            MenuAutoStart.IsChecked = status.Enabled ?? false;
            MenuAutoStart.IsEnabled = status.Enabled.HasValue;
            MenuAutoStart.ToolTip = status.Error;
        }

        private void ShowSettingsError(string? message) =>
            MessageBox.Show(this, message ?? "Your settings could not be saved.",
                "TrafficSense", MessageBoxButton.OK, MessageBoxImage.Warning);

        private void MenuTheme_Click(object sender, RoutedEventArgs e)
        {
            string previousTheme = _settings.Theme;
            _settings.Theme = previousTheme == "Dark" ? "Light" : "Dark";
            var result = SettingsManager.Save(_settings);
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
