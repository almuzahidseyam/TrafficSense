using System.Windows;
using System.Security.Principal;
using System.Windows.Threading;

namespace TrafficSense
{
    public partial class App : Application
    {
        private SingleInstanceGate? _instance;
        private Mutex? _installerMutex;
        private TrayController? _tray;
        private volatile bool _exiting;
        private readonly string? _instanceName;
        private readonly Func<MainWindow>? _createWindow;
        private readonly DiagnosticLog? _diagnostics;
        private readonly Action<string, string> _showFatal;
        private int _fatalErrorStarted;
        private UiLanguage _localization = new();

        public App()
        {
            _diagnostics = DiagnosticLog.Default;
            _showFatal = ShowFatalDialog;
        }

        // Allows lifecycle probes to coordinate under an isolated name without
        // activating or taking ownership from the user's running application.
        internal App(string instanceName, Func<MainWindow>? createWindow = null,
            DiagnosticLog? diagnostics = null, Action<string, string>? showFatal = null)
        {
            _instanceName = instanceName;
            _createWindow = createWindow;
            _diagnostics = diagnostics;
            _showFatal = showFatal ?? ShowFatalDialog;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            try
            {
                base.OnStartup(e);
                using var identity = WindowsIdentity.GetCurrent();
                // Local scopes coordination to the sign-in session; the SID
                // also keeps different Windows users independent of one another.
                bool sensorBridge = e.Args.Length == 1 && e.Args[0] == "--sensor-bridge";
                _instance = new SingleInstanceGate(_instanceName ?? @"Local\TrafficSense." + identity.User!.Value + (sensorBridge ? ".Sensors" : ""));
                if (!_instance.IsPrimary)
                {
                    _instance.NotifyPrimary();
                    Shutdown();
                    return;
                }
                if (_instanceName == null)
                    _installerMutex = new Mutex(false, @"Local\TrafficSense.Installer." + Environment.UserName.ToLowerInvariant());
                if (sensorBridge)
                {
                    _localization = new UiLanguage(SettingsManager.Load().Language);
                    var bridge = new SensorBridgeWindow(_localization); MainWindow = bridge; bridge.Show();
                    _instance.Listen(() => Dispatcher.BeginInvoke(new Action(() =>
                    { if (!_exiting) { if (bridge.WindowState == WindowState.Minimized) bridge.WindowState = WindowState.Normal; bridge.Show(); bridge.Activate(); } })));
                    return;
                }

                var window = _createWindow?.Invoke() ?? new MainWindow();
                _localization = window.Localization;
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
                FailAndClose("Startup", error, TextKey.StartFailure);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _exiting = true;
            DisposeResource(_tray, "TrayCleanup");
            DisposeResource(_instance, "InstanceCleanup");
            DisposeResource(_installerMutex, "InstallerMutexCleanup");
            DispatcherUnhandledException -= App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Prevent WPF's default dialog, then stop rather than continue with
            // unknown UI state. Expected recoverable errors are handled locally.
            e.Handled = true;
            FailAndClose("DispatcherUnhandled", e.Exception, TextKey.FatalError);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // The runtime is terminating. Persist synchronously, without showing
            // a worker-thread dialog or waiting on a potentially broken UI thread.
            _diagnostics?.Write("BackgroundUnhandled", e.ExceptionObject as Exception
                ?? new Exception("Unhandled non-Exception object."), fatal: true);
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            _diagnostics?.Write("UnobservedTask", e.Exception);
            e.SetObserved();
        }

        private static void ShowFatalDialog(string message, string title) =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

        private void DisposeResource(IDisposable? resource, string category)
        {
            try { resource?.Dispose(); }
            catch (Exception error) { _diagnostics?.Write(category, error); }
        }

        private void FailAndClose(string category, Exception error, TextKey headline)
        {
            if (Interlocked.Exchange(ref _fatalErrorStarted, 1) != 0) return;
            _exiting = true;
            string? path = _diagnostics?.Write(category, error, fatal: true);
            try
            {
                (MainWindow as MainWindow)?.PrepareForFatalShutdown();
                DisposeResource(_tray, "TrayCleanup");
                string message = _localization[headline] + "\n\n" + _localization[TextKey.AppWillClose]
                    + "\n\n" + (path == null ? _localization[TextKey.LogUnavailable]
                        : _localization[TextKey.LogSaved] + "\n" + path);
                _showFatal(message, _localization[TextKey.ErrorTitle]);
            }
            catch (Exception reportingError) { _diagnostics?.Write("FatalReporting", reportingError, fatal: true); }
            finally { Shutdown(1); }
        }
    }
}
