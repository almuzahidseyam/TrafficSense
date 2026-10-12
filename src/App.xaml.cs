using System.Windows;
using System.Security.Principal;
using System.Windows.Threading;

namespace TrafficSense
{
    public partial class App : Application
    {
        private SingleInstanceGate? _instance;
        private TrayController? _tray;
        private volatile bool _exiting;
        private readonly string? _instanceName;

        public App() { }

        // Allows lifecycle probes to coordinate under an isolated name without
        // activating or taking ownership from the user's running application.
        internal App(string instanceName) => _instanceName = instanceName;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                // Local scopes coordination to the sign-in session; the SID
                // also keeps different Windows users independent of one another.
                _instance = new SingleInstanceGate(_instanceName ?? @"Local\TrafficSense." + identity.User!.Value);
                if (!_instance.IsPrimary)
                {
                    _instance.NotifyPrimary();
                    Shutdown();
                    return;
                }

                var window = new MainWindow();
                MainWindow = window;
                _tray = new TrayController(window);
                window.Show();
                _instance.Listen(() =>
                {
                    if (!_exiting)
                        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                        {
                            if (!_exiting) window.ShowWidget();
                        }));
                });
            }
            catch (Exception error)
            {
                MessageBox.Show($"TrafficSense could not start.\n\n{error.Message}",
                    "TrafficSense", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _exiting = true;
            _tray?.Dispose();
            _instance?.Dispose();
            DispatcherUnhandledException -= App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"UI Crash: {e.Exception.Message}\n\n{e.Exception.StackTrace}", "TrafficSense Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show($"Background Crash: {ex?.Message}\n\n{ex?.StackTrace}", "TrafficSense Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
