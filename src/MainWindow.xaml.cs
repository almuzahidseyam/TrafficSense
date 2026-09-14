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
        private DispatcherTimer _timer;
        private long _lastBytesReceived = 0;
        private long _lastBytesSent = 0;
        private bool _isInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            
            // Set initial position to bottom right corner
            this.Loaded += (s, e) => 
            {
                var desktopWorkingArea = SystemParameters.WorkArea;
                this.Left = desktopWorkingArea.Right - this.Width - 20;
                this.Top = desktopWorkingArea.Bottom - this.Height - 20;
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

            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && 
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);

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
    }
}
