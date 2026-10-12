using System.Net;
using System.Net.Http;
using System.Text;
using TrafficSense;
using Xunit;

public sealed class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.3.1", (int)UpdateStatus.Available)]
    [InlineData("v1.10.0", (int)UpdateStatus.Available)]
    [InlineData("v2.0.0", (int)UpdateStatus.Available)]
    [InlineData("v1.3.0", (int)UpdateStatus.UpToDate)]
    [InlineData("1.3.0+other-build", (int)UpdateStatus.UpToDate)]
    [InlineData("v1.2.9", (int)UpdateStatus.UpToDate)]
    public async Task ComparesVersionsNumericallyIgnoringBuildMetadata(string tag, int expected)
    {
        using var client = Client((request, _) =>
        {
            Assert.Equal(UpdateChecker.Endpoint, request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("TrafficSense/1.3.0", request.Headers.UserAgent.ToString());
            Assert.Equal("application/vnd.github+json", request.Headers.Accept.ToString());
            Assert.Equal("2026-03-10", request.Headers.GetValues("X-GitHub-Api-Version").Single());
            Assert.Null(request.Headers.Authorization);
            return Task.FromResult(Json(Release(tag)));
        });
        var result = await new UpdateChecker(client, new Version(1, 3, 0, 0)).CheckAsync(default);
        Assert.Equal((UpdateStatus)expected, result.Status);
        if (expected == (int)UpdateStatus.Available)
            Assert.Equal(new Uri($"{UpdateChecker.RepositoryUrl}/releases/tag/{Uri.EscapeDataString(tag)}"), result.ReleasePage);
        else Assert.Null(result.ReleasePage);
    }

    [Theory]
    [InlineData("v1.4.0-beta.1")]
    [InlineData("v01.4.0")]
    [InlineData("v1.4")]
    [InlineData("v1.4.0.0")]
    [InlineData("v1.4.0/../../other")]
    [InlineData("v1.4.0?target=bad")]
    [InlineData("v1.4.0\n")]
    [InlineData("v999999999999999.0.0")]
    public async Task RejectsInvalidOrPrereleaseTags(string tag)
    {
        using var client = Client((_, _) => Task.FromResult(Json(Release(tag))));
        var result = await new UpdateChecker(client, new Version(1, 3, 0)).CheckAsync(default);
        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.NotNull(result.Error);
        Assert.Null(result.ReleasePage);
    }

    [Theory]
    [InlineData("{\"tag_name\":\"v1.4.0\",\"draft\":true,\"prerelease\":false}")]
    [InlineData("{\"tag_name\":\"v1.4.0\",\"draft\":false,\"prerelease\":true}")]
    public async Task DoesNotOfferDraftsOrPrereleases(string json)
    {
        using var client = Client((_, _) => Task.FromResult(Json(json)));
        Assert.Equal(UpdateStatus.NoRelease, (await new UpdateChecker(client).CheckAsync(default)).Status);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"tag_name\":\"v1.4.0\"}")]
    [InlineData("{\"tag_name\":7,\"draft\":false,\"prerelease\":false}")]
    public async Task HandlesMalformedOrIncompleteResponses(string json)
    {
        using var client = Client((_, _) => Task.FromResult(Json(json)));
        var result = await new UpdateChecker(client).CheckAsync(default);
        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.Null(result.ReleasePage);
    }

    [Theory]
    [InlineData(404, (int)UpdateStatus.NoRelease)]
    [InlineData(429, (int)UpdateStatus.RateLimited)]
    [InlineData(403, (int)UpdateStatus.Failed)]
    [InlineData(500, (int)UpdateStatus.Failed)]
    [InlineData(301, (int)UpdateStatus.Failed)]
    public async Task HandlesHttpStatuses(int status, int expected)
    {
        using var client = Client((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        Assert.Equal((UpdateStatus)expected, (await new UpdateChecker(client).CheckAsync(default)).Status);
    }

    [Fact]
    public async Task RecognizesForbiddenRateLimit()
    {
        using var client = Client((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
            response.Headers.Add("X-RateLimit-Remaining", "0");
            return Task.FromResult(response);
        });
        Assert.Equal(UpdateStatus.RateLimited, (await new UpdateChecker(client).CheckAsync(default)).Status);
    }

    [Fact]
    public async Task DoesNotTrustRemoteDownloadUrls()
    {
        using var client = Client((_, _) => Task.FromResult(Json(
            "{\"tag_name\":\"v1.4.0\",\"draft\":false,\"prerelease\":false,\"html_url\":\"file:///evil.exe\"}")));
        var result = await new UpdateChecker(client, new Version(1, 3, 0)).CheckAsync(default);
        Assert.Equal(new Uri(UpdateChecker.RepositoryUrl + "/releases/tag/v1.4.0"), result.ReleasePage);
    }

    [Fact]
    public async Task ReportsOfflineFailureWithoutThrowing()
    {
        using var client = Client((_, _) => throw new HttpRequestException("Offline"));
        var result = await new UpdateChecker(client).CheckAsync(default);
        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.IsType<HttpRequestException>(result.Error);
    }

    [Fact]
    public async Task BoundsTimeoutAndDistinguishesUserCancellation()
    {
        using var client = Client(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Json(Release("v1.4.0"));
        });
        var timeout = await new UpdateChecker(client, timeout: TimeSpan.FromMilliseconds(25)).CheckAsync(default);
        Assert.Equal(UpdateStatus.Failed, timeout.Status);
        Assert.IsAssignableFrom<OperationCanceledException>(timeout.Error);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UpdateChecker(client).CheckAsync(cancellation.Token));
    }

    private static string Release(string tag) => System.Text.Json.JsonSerializer.Serialize(new { tag_name = tag, draft = false, prerelease = false });
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => new(new Handler(send));
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
