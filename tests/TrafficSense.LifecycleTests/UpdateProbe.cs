using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TrafficSense;

internal static class UpdateProbe
{
    internal static int CheckLive()
    {
        var result = new UpdateChecker().CheckAsync(default).GetAwaiter().GetResult();
        Check(result.Status is UpdateStatus.UpToDate or UpdateStatus.Available, $"Live GitHub check failed: {result.Status}, {result.Error}");
        Console.WriteLine($"PASS: live GitHub release check: {result.Status} {result.Version}");
        return 0;
    }

    internal static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "TrafficSense.UpdateTests", Guid.NewGuid().ToString("N"));
        var log = new DiagnosticLog(root);
        int requests = 0;
        bool browserFails = false, passed = false;
        var opened = new List<Uri>();
        Func<CancellationToken, Task<HttpResponseMessage>> send = _ => Task.FromResult(Release("v1.3.0"));
        using var client = new HttpClient(new Handler(token => { requests++; return send(token); }));
        var updates = new UpdateChecker(client, new Version(1, 3, 0));
        var app = new App(@"Local\TrafficSense.Updates." + Guid.NewGuid().ToString("N"),
            () => new MainWindow(new AppSettings(), _ => new SettingsSaveResult(true),
                diagnostics: log, updates: updates, openReleasePage: uri =>
                {
                    if (browserFails) throw new Win32Exception("Simulated browser launch failure.");
                    opened.Add(uri);
                }));
        app.InitializeComponent();
        foreach (Type type in new[] { typeof(MainWindow), typeof(AboutWindow) })
        {
            var style = new Style(type);
            style.Setters.Add(new Setter(UIElement.OpacityProperty, 0d));
            style.Setters.Add(new Setter(Window.ShowActivatedProperty, false));
            app.Resources.Add(type, style);
        }
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                var window = (MainWindow)app.MainWindow;
                window.Hide();
                window.ShowAbout();
                var about = app.Windows.OfType<AboutWindow>().Single();
                Check(requests == 0, "Opening About made an automatic network request.");
                var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                send = _ => pending.Task;
                Click(about, "CheckUpdatesButton");
                Click(about, "CheckUpdatesButton");
                Check(requests == 1 && !Button(about, "CheckUpdatesButton").IsEnabled, "Duplicate checks were not prevented.");
                window.ChangeLanguage("bn");
                Check(Status(about).Contains("খোঁজা হচ্ছে"), "Pending update status did not translate.");
                pending.SetResult(Release("v1.4.0"));
                await Completed(about);
                Check(Status(about).Contains("1.4.0") && Status(about).Contains("পাওয়া গেছে") && !window.IsVisible,
                    "Available update did not display or showed the hidden widget.");
                Check(((TextBox)about.FindName("ReleasePageValue")).Text == UpdateChecker.RepositoryUrl + "/releases/tag/v1.4.0", "Wrong release link.");
                AboutProbe.Capture(about, "update-bn");
                window.ChangeLanguage("en");
                Check(Status(about) == "Version 1.4.0 is available.", "Completed update status did not translate.");
                AboutProbe.Capture(about, "update-en");
                Click(about, "OpenReleaseButton");
                Check(opened.Count == 1 && opened[0].AbsoluteUri.EndsWith("/v1.4.0"), "Release button did not open the expected page.");
                browserFails = true;
                Click(about, "OpenReleaseButton");
                Check(Status(about).Contains("could not be opened") && File.ReadAllText(log.FilePath).Contains("OpenReleasePage"),
                    "Browser failure was not recoverable and logged.");
                window.ChangeLanguage("bn");
                Check(Status(about).Contains("খোলা যায়নি"), "Browser failure did not translate.");
                browserFails = false;
                Click(about, "OpenReleaseButton");
                Check(opened.Count == 2 && Status(about).Contains("পাওয়া গেছে"), "Browser retry did not recover.");
                send = _ =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
                    response.Headers.Add("X-RateLimit-Remaining", "0");
                    return Task.FromResult(response);
                };
                Click(about, "CheckUpdatesButton");
                await Completed(about);
                Check(Status(about).Contains("সীমিত") && ((StackPanel)about.FindName("UpdateDownloadPanel")).Visibility == Visibility.Collapsed,
                    "Rate limiting retained a stale release link or did not translate.");
                send = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                Click(about, "CheckUpdatesButton");
                await Completed(about);
                Check(Status(about).Contains("রিলিজ পাওয়া যায়নি"), "Missing release status was incorrect.");
                send = _ => Task.FromResult(Release("v1.3.0"));
                Click(about, "CheckUpdatesButton");
                await Completed(about);
                Check(Status(about).Contains("হালনাগাদ"), "Current version was incorrectly offered as an update.");
                send = _ => throw new HttpRequestException("Simulated offline check.");
                Click(about, "CheckUpdatesButton");
                await Completed(about);
                Check(Status(about).Contains("খোঁজা যায়নি") && File.ReadAllText(log.FilePath).Contains("UpdateCheck") && window.IsEnabled,
                    "Offline failure did not preserve app operation and record diagnostics.");

                bool cancelled = false;
                pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
                send = token => { token.Register(() => cancelled = true); return pending.Task; };
                Click(about, "CheckUpdatesButton");
                string closedText = Status(about);
                about.Close();
                Check(cancelled && !window.IsVisible, "Closing About did not cancel its request or showed the widget.");
                pending.SetResult(Release("v9.0.0"));
                await Task.Delay(30);
                Check(Status(about) == closedText, "Late completion updated the closed About window.");
                window.ShowAbout();
                var reopened = app.Windows.OfType<AboutWindow>().Single();
                Check(Button(reopened, "CheckUpdatesButton").IsEnabled && Status(reopened).Contains("নতুন সংস্করণ খুঁজুন"),
                    "Reopened About retained a stale result or disabled button.");
                cancelled = false;
                pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Click(reopened, "CheckUpdatesButton");
                window.PrepareForFatalShutdown();
                Check(cancelled && !reopened.IsVisible, "Fatal shutdown did not cancel and close the active update check.");
                pending.SetResult(Release("v9.0.0"));
                await Task.Delay(30);
                passed = true;
                window.Close();
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                app.Shutdown(1);
            }
        }));
        try
        {
            int exit = app.Run();
            Check(passed && exit == 0, "Update lifecycle probe failed.");
            Console.WriteLine("PASS: manual update checks, duplicate suppression, live translations, trusted release page/retry, offline/rate-limit handling, hidden widget, close/reopen cancellation, late completion and fatal cleanup.");
            return 0;
        }
        finally
        {
            string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TrafficSense.UpdateTests")) + Path.DirectorySeparatorChar;
            Check(Path.GetFullPath(root).StartsWith(parent, StringComparison.OrdinalIgnoreCase), "Unsafe temporary cleanup path.");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static Button Button(AboutWindow window, string name) => (Button)window.FindName(name);
    private static void Click(AboutWindow window, string name) => Button(window, name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static string Status(AboutWindow window) => ((TextBlock)window.FindName("UpdateStatusText")).Text;
    private static HttpResponseMessage Release(string tag) => new(HttpStatusCode.OK)
    { Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { tag_name = tag, draft = false, prerelease = false })) };
    private static async Task Completed(AboutWindow window)
    {
        var watch = Stopwatch.StartNew();
        while (!Button(window, "CheckUpdatesButton").IsEnabled)
        {
            Check(watch.Elapsed < TimeSpan.FromSeconds(3), "Update check did not complete.");
            await Task.Delay(10);
        }
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Handler(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(cancellationToken);
    }
}
