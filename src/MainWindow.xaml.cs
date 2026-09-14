using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace TrafficSense
{
    public partial class MainWindow : FluentWindow
    {
        private DispatcherTimer? _timer;
        private long _lastBytesReceived = 0;
        private long _lastBytesSent = 0;
        private bool _isInitialized = false;
        private AppSettings _settings;

        public MainWindow()
        {
            InitializeComponent();
            _settings = SettingsManager.Load();
            
            this.Loaded += (s, e) => 
            {
                if (_settings.WindowLeft == -1 || _settings.WindowTop == -1)
                {
                    // Default to bottom right
                    var desktopWorkingArea = SystemParameters.WorkArea;
                    this.Left = desktopWorkingArea.Right - this.Width - 20;
                    this.Top = desktopWorkingArea.Bottom - this.Height - 20;
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
        }

        private void InitializeNetworkMonitor()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            long currentBytesReceived = 0;
            long currentBytesSent = 0;

            // Only get interfaces that have a gateway (usually actual internet connections)
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && 
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                              nic.GetIPProperties().GatewayAddresses.Count > 0);

            // If no interface with gateway found, fallback to all active non-loopback interfaces
            if (!interfaces.Any())
            {
                interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up && 
                                  nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                  !nic.Description.ToLower().Contains("wsl") && 
                                  !nic.Description.ToLower().Contains("hyper-v") &&
                                  !nic.Description.ToLower().Contains("virtual"));
            }

            foreach (var nic in interfaces)
            {
                var stats = nic.GetIPv4Statistics();
                currentBytesReceived += stats.BytesReceived;
                currentBytesSent += stats.BytesSent;
            }

            if (_isInitialized)
            {
                long downloadSpeed = currentBytesReceived - _lastBytesReceived;
                long uploadSpeed = currentBytesSent - _lastBytesSent;

                TxtDownload.Text = FormatSpeed(downloadSpeed);
                TxtUpload.Text = FormatSpeed(uploadSpeed);
            }
            else
            {
                _isInitialized = true;
            }

            _lastBytesReceived = currentBytesReceived;
            _lastBytesSent = currentBytesSent;
        }

        private string FormatSpeed(long bytesPerSecond)
        {
            if (bytesPerSecond < 0) bytesPerSecond = 0;
            
            if (bytesPerSecond > 1024 * 1024 * 1024)
                return $"{(bytesPerSecond / (1024.0 * 1024.0 * 1024.0)):F2} GB/s";
            if (bytesPerSecond > 1024 * 1024)
                return $"{(bytesPerSecond / (1024.0 * 1024.0)):F2} MB/s";
            if (bytesPerSecond > 1024)
                return $"{(bytesPerSecond / 1024.0):F2} KB/s";
            
            return $"{bytesPerSecond} B/s";
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
            if (_settings.Theme == "Light")
            {
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light);
                TxtDownload.Foreground = System.Windows.Media.Brushes.Black;
                TxtUpload.Foreground = System.Windows.Media.Brushes.Black;
            }
            else
            {
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
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
