using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace TrafficSense
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? _timer;
        private readonly NetworkSpeedCalculator _speedCalculator = new();
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
            _speedCalculator.Update(NetworkSnapshotReader.Read(), _samplingClock.Elapsed);
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var snapshots = NetworkSnapshotReader.Read();
            var speed = _speedCalculator.Update(snapshots, _samplingClock.Elapsed);
            TxtDownload.Text = SpeedFormatter.Format(speed.DownloadBytesPerSecond);
            TxtUpload.Text = SpeedFormatter.Format(speed.UploadBytesPerSecond);
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
