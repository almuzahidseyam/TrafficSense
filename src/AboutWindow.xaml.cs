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
    private readonly UpdateChecker _updates;
    private readonly Action<Uri> _openReleasePage;
    private readonly CancellationTokenSource _closeCancellation = new();
    private UpdateCheckResult? _updateResult;
    private bool _checking, _closed, _releasePageFailed;

    internal AboutWindow(UiLanguage language, DiagnosticLog diagnostics,
        Action<string>? reportError = null, Action<string>? launchFolder = null,
        UpdateChecker? updates = null, Action<Uri>? openReleasePage = null)
    {
        _language = language;
        _diagnostics = diagnostics;
        _reportError = reportError;
        _launchFolder = launchFolder ?? LaunchFolder;
        _updates = updates ?? new UpdateChecker();
        _openReleasePage = openReleasePage ?? OpenReleasePage;
        InitializeComponent();
        VersionValue.Text = typeof(MainWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "—";
        LogFolderValue.Text = diagnostics.DirectoryPath;
        ApplyLanguage();
        _language.Changed += LanguageChanged;
        Closed += (_, _) =>
        {
            _closed = true;
            _language.Changed -= LanguageChanged;
            _closeCancellation.Cancel();
            _closeCancellation.Dispose();
        };
    }

    private void LanguageChanged(object? sender, EventArgs e) => ApplyLanguage();

    private void ApplyLanguage()
    {
        Title = _language[TextKey.About];
        VersionLabel.Text = _language[TextKey.Version];
        LogFolderLabel.Text = _language[TextKey.LogFolder];
        OpenLogFolderButton.Content = _language[TextKey.OpenLogFolder];
        CloseButton.Content = _language[TextKey.Close];
        ApplyUpdateLanguage();
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

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (_checking || _closed) return;
        _checking = true;
        _updateResult = null;
        _releasePageFailed = false;
        CheckUpdatesButton.IsEnabled = false;
        ApplyUpdateLanguage();
        var cancellation = _closeCancellation.Token;
        try
        {
            var result = await _updates.CheckAsync(cancellation);
            if (_closed) return;
            _updateResult = result;
            if (result.Error != null) _diagnostics.Write("UpdateCheck", result.Error);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (!_closed)
            {
                _checking = false;
                CheckUpdatesButton.IsEnabled = true;
                ApplyUpdateLanguage();
            }
        }
    }

    private void ApplyUpdateLanguage()
    {
        CheckUpdatesButton.Content = _language[TextKey.CheckUpdates];
        OpenReleaseButton.Content = _language[TextKey.OpenRelease];
        TextKey status = _checking ? TextKey.UpdateChecking : _releasePageFailed ? TextKey.OpenReleaseFailed
            : _updateResult?.Status switch
            {
                UpdateStatus.Available => TextKey.UpdateAvailable,
                UpdateStatus.UpToDate => TextKey.UpdateCurrent,
                UpdateStatus.NoRelease => TextKey.UpdateNoRelease,
                UpdateStatus.RateLimited => TextKey.UpdateRateLimited,
                UpdateStatus.Failed => TextKey.UpdateCheckFailed,
                _ => TextKey.UpdateHint
            };
        UpdateStatusText.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
            _language[status], _updateResult?.Version);
        bool available = _updateResult?.Status == UpdateStatus.Available;
        UpdateDownloadPanel.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        ReleasePageValue.Text = available ? _updateResult!.ReleasePage!.AbsoluteUri : "";
    }

    private void OpenRelease_Click(object sender, RoutedEventArgs e)
    {
        if (_closed || _updateResult?.ReleasePage is not { } page) return;
        try
        {
            _openReleasePage(page);
            _releasePageFailed = false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            _diagnostics.Write("OpenReleasePage", error);
            _releasePageFailed = true;
        }
        ApplyUpdateLanguage();
    }

    private static void OpenReleasePage(Uri page)
    {
        using var process = Process.Start(new ProcessStartInfo(page.AbsoluteUri) { UseShellExecute = true });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
