using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

namespace TrafficSense;

public partial class AboutWindow : Window
{
    private readonly UiLanguage _language;
    private readonly DiagnosticLog _diagnostics;
    private readonly Action<string>? _reportError;
    private readonly Action<string> _launchFolder;

    internal AboutWindow(UiLanguage language, DiagnosticLog diagnostics,
        Action<string>? reportError = null, Action<string>? launchFolder = null)
    {
        _language = language;
        _diagnostics = diagnostics;
        _reportError = reportError;
        _launchFolder = launchFolder ?? LaunchFolder;
        InitializeComponent();
        VersionValue.Text = typeof(MainWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "—";
        LogFolderValue.Text = diagnostics.DirectoryPath;
        ApplyLanguage();
        _language.Changed += LanguageChanged;
        Closed += (_, _) => _language.Changed -= LanguageChanged;
    }

    private void LanguageChanged(object? sender, EventArgs e) => ApplyLanguage();

    private void ApplyLanguage()
    {
        Title = _language[TextKey.About];
        VersionLabel.Text = _language[TextKey.Version];
        LogFolderLabel.Text = _language[TextKey.LogFolder];
        OpenLogFolderButton.Content = _language[TextKey.OpenLogFolder];
        CloseButton.Content = _language[TextKey.Close];
    }

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // A fresh install may not have recorded an error yet.
            Directory.CreateDirectory(_diagnostics.DirectoryPath);
            _launchFolder(_diagnostics.DirectoryPath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            _diagnostics.Write("OpenLogFolder", error);
            string message = _language[TextKey.LogFolderOpenFailed];
            if (_reportError != null) _reportError(message);
            else MessageBox.Show(this, message, _language[TextKey.ErrorTitle],
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void LaunchFolder(string path)
    {
        using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
