using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace TrafficSense
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? _timer;
        private readonly NetworkSpeedCalculator _speedCalculator = new();
        private readonly Stopwatch _samplingClock = Stopwatch.StartNew();
        private AppSettings _settings;

        public MainWindow()
        {
            InitializeComponent();
            _settings = SettingsManager.Load();
            
            this.Loaded += (s, e) => 
            {
                if (_settings.WindowLeft == -1 || _settings.WindowTop == -1 ||
                    !double.IsFinite(_settings.WindowLeft) || !double.IsFinite(_settings.WindowTop))
                {
                    // Default to bottom right
                    var desktopWorkingArea = SystemParameters.WorkArea;
                    // Width/Height are NaN when SizeToContent determines the size.
                    this.Left = Math.Max(desktopWorkingArea.Left, desktopWorkingArea.Right - this.ActualWidth - 20);
                    this.Top = Math.Max(desktopWorkingArea.Top, desktopWorkingArea.Bottom - this.ActualHeight - 20);
                }
                else
                {
                    this.Left = _settings.WindowLeft;
                    this.Top = _settings.WindowTop;
                }

                MenuAutoStart.IsChecked = _settings.AutoStart;
                ApplyTheme();
            };

            InitializeNetworkMonitor();
            Closed += (_, _) =>
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Tick -= Timer_Tick;
                }
            };
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
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save position before closing
            _settings.WindowLeft = this.Left;
            _settings.WindowTop = this.Top;
            SettingsManager.Save(_settings);
        }

        private void MenuAutoStart_Click(object sender, RoutedEventArgs e)
        {
            _settings.AutoStart = MenuAutoStart.IsChecked;
            SettingsManager.Save(_settings);
        }

        private void MenuTheme_Click(object sender, RoutedEventArgs e)
        {
            _settings.Theme = _settings.Theme == "Dark" ? "Light" : "Dark";
            SettingsManager.Save(_settings);
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
