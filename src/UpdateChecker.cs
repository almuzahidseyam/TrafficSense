using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TrafficSense;

internal enum UpdateStatus { UpToDate, Available, NoRelease, RateLimited, Failed }

internal sealed record UpdateCheckResult(UpdateStatus Status, string? Version = null,
    Uri? ReleasePage = null, Exception? Error = null);

internal sealed class UpdateChecker
{
    internal const string RepositoryUrl = "https://github.com/almuzahidseyam/TrafficSense";
    internal const string Endpoint = "https://api.github.com/repos/almuzahidseyam/TrafficSense/releases/latest";
    private static readonly HttpClient SharedClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(10)
    }) { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 256 * 1024 };
    private readonly HttpClient _client;
    private readonly Version _current;
    private readonly TimeSpan _timeout;

    internal UpdateChecker(HttpClient? client = null, Version? currentVersion = null, TimeSpan? timeout = null)
    {
        _client = client ?? SharedClient;
        var version = currentVersion ?? typeof(UpdateChecker).Assembly.GetName().Version!;
        _current = new Version(version.Major, version.Minor, Math.Max(0, version.Build));
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    internal async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            request.Headers.UserAgent.ParseAdd($"TrafficSense/{_current}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
            using var response = await _client.SendAsync(request, deadline.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound) return new(UpdateStatus.NoRelease);
            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                (response.StatusCode == HttpStatusCode.Forbidden &&
                    (response.Headers.RetryAfter != null ||
                     (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.Contains("0")))))
                return new(UpdateStatus.RateLimited);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(deadline.Token).ConfigureAwait(false);
            var release = JsonSerializer.Deserialize<Release>(json);
            if (release == null || release.Draft == null || release.Prerelease == null)
                throw new InvalidDataException("Update response is missing release metadata.");
            if (release.Draft.Value || release.Prerelease.Value) return new(UpdateStatus.NoRelease);
            if (!TryStableVersion(release.Tag, out var latest))
                throw new InvalidDataException("Update response does not contain a stable version tag.");
            // Construct the repository's release URL rather than trusting a URL
            // supplied by the response. Nothing is downloaded or executed here.
            return latest > _current
                ? new(UpdateStatus.Available, latest.ToString(3),
                    new Uri($"{RepositoryUrl}/releases/tag/{Uri.EscapeDataString(release.Tag!)}"))
                : new(UpdateStatus.UpToDate);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is HttpRequestException or IOException or InvalidDataException
            or JsonException or OperationCanceledException)
        {
            return new(UpdateStatus.Failed, Error: error);
        }
    }

    private static bool TryStableVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (tag == null || tag.Length > 128) return false;
        var match = Regex.Match(tag, @"\Av?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z");
        return match.Success && Version.TryParse($"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}", out version!);
    }

    private sealed record Release(
        [property: JsonPropertyName("tag_name")] string? Tag,
        [property: JsonPropertyName("draft")] bool? Draft,
        [property: JsonPropertyName("prerelease")] bool? Prerelease);
}
