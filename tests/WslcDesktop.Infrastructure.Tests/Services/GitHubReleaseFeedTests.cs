using System.Net;
using System.Text;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class GitHubReleaseFeedTests : IDisposable
{
    private const string Latest = "https://api.github.com/repos/yzhoholiev/wslc-desktop/releases/latest";
    private const string List = "https://api.github.com/repos/yzhoholiev/wslc-desktop/releases?per_page=20";
    private const string Asset = "https://github.com/yzhoholiev/wslc-desktop/releases/download/v1.2.0/WslcDesktop-win-x64.exe";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "WslcDesktop.Tests", $"feed-{Guid.NewGuid():N}");
    private readonly StubHandler _handler = new();

    public GitHubReleaseFeedTests() => Directory.CreateDirectory(_folder);

    private string DownloadPath => Path.Combine(_folder, "download.bin");

    public void Dispose() => Directory.Delete(_folder, true);

    [Fact]
    public async Task FindLatestAsync_MapsTheLatestRelease()
    {
        _handler.Json(Latest, ReleaseJson("v1.2.0"));

        AppRelease? release = await Feed().FindLatestAsync(false, TestContext.Current.CancellationToken);

        Assert.NotNull(release);
        Assert.Equal("1.2.0", release.Version.ToString());
        Assert.Equal(new Uri("https://github.com/yzhoholiev/wslc-desktop/releases/tag/v1.2.0"), release.Page);
        Assert.Equal(new Uri(Asset), release.Asset);
        Assert.Equal(5, release.AssetSize);
        Assert.Equal(new Uri("https://github.com/yzhoholiev/wslc-desktop/releases/download/v1.2.0/SHA256SUMS.txt"), release.Checksums);
    }

    [Fact]
    public async Task FindLatestAsync_PicksTheHighestVersion_WithPreReleases()
    {
        _handler.Json(List, $"[{ReleaseJson("v1.1.1-rc.1")},{ReleaseJson("v1.1.0")},{ReleaseJson("v1.1.1-rc.2")}]");

        AppRelease? release = await Feed().FindLatestAsync(true, TestContext.Current.CancellationToken);

        Assert.Equal("1.1.1-rc.2", release?.Version.ToString());
    }

    [Fact]
    public async Task FindLatestAsync_SkipsDraftsAndTagsThatAreNotVersions()
    {
        _handler.Json(List, $"[{ReleaseJson("nightly")},{ReleaseJson("v9.0.0", draft: true)},{ReleaseJson("v1.2.0")}]");

        AppRelease? release = await Feed().FindLatestAsync(true, TestContext.Current.CancellationToken);

        Assert.Equal("1.2.0", release?.Version.ToString());
    }

    [Fact]
    public async Task FindLatestAsync_ReturnsNull_WhenTheLatestTagIsNotAVersion()
    {
        _handler.Json(Latest, ReleaseJson("nightly"));

        Assert.Null(await Feed().FindLatestAsync(false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FindLatestAsync_ReturnsNull_WhenThereIsNoRelease()
    {
        _handler.Respond(Latest, HttpStatusCode.NotFound);

        Assert.Null(await Feed().FindLatestAsync(false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FindLatestAsync_LeavesTheAssetOut_WhenItsUrlIsNotOnGitHub()
    {
        _handler.Json(Latest, ReleaseJson("v1.2.0").Replace(Asset, "https://example.com/WslcDesktop-win-x64.exe"));

        AppRelease? release = await Feed().FindLatestAsync(false, TestContext.Current.CancellationToken);

        Assert.NotNull(release);
        Assert.Null(release.Asset);
        Assert.NotNull(release.Checksums);
    }

    [Fact]
    public async Task FindLatestAsync_LeavesTheAssetOut_WhenTheReleaseHasNoneForThisArchitecture()
    {
        _handler.Json(Latest, ReleaseJson("v1.2.0"));

        AppRelease? release = await Feed("WslcDesktop-win-arm64.exe").FindLatestAsync(false, TestContext.Current.CancellationToken);

        Assert.NotNull(release);
        Assert.Null(release.Asset);
    }

    [Fact]
    public async Task FindLatestAsync_SendsOnlyTheUserAgentAndAccept()
    {
        _handler.Json(Latest, ReleaseJson("v1.2.0"));

        await Feed().FindLatestAsync(false, TestContext.Current.CancellationToken);

        (HttpMethod method, Uri url, string[] headers, bool hasBody) = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Get, method);
        Assert.Equal(new Uri(Latest), url);
        Assert.Equal(["Accept: application/vnd.github+json", "User-Agent: WslcDesktop/1.1.1-10"], headers);
        Assert.False(hasBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "GitHub is limiting requests. Try again later.")]
    [InlineData(HttpStatusCode.TooManyRequests, "GitHub is limiting requests. Try again later.")]
    [InlineData(HttpStatusCode.InternalServerError, "GitHub answered with an error (500).")]
    public async Task FindLatestAsync_ExplainsAnErrorStatus(HttpStatusCode status, string message)
    {
        _handler.Respond(Latest, status);

        UpdateException failure = await Assert.ThrowsAsync<UpdateException>(() => Feed().FindLatestAsync(false, TestContext.Current.CancellationToken));

        Assert.Equal(message, failure.Message);
    }

    [Theory]
    [InlineData("<html>rate limited</html>")]
    [InlineData("{\"tag_name\":")]
    [InlineData("null")]
    [InlineData("")]
    public async Task FindLatestAsync_ExplainsAnAnswerThatIsNotARelease(string body)
    {
        _handler.Json(Latest, body);

        UpdateException failure = await Assert.ThrowsAsync<UpdateException>(() => Feed().FindLatestAsync(false, TestContext.Current.CancellationToken));

        Assert.Equal("GitHub answered with something unexpected.", failure.Message);
    }

    [Fact]
    public async Task FindLatestAsync_ExplainsANetworkFailure()
    {
        _handler.Fail(Latest, new HttpRequestException("No such host is known."));

        UpdateException failure = await Assert.ThrowsAsync<UpdateException>(() => Feed().FindLatestAsync(false, TestContext.Current.CancellationToken));

        Assert.Equal("GitHub is not reachable.", failure.Message);
    }

    [Fact]
    public async Task ReadChecksumsAsync_ReturnsTheFile()
    {
        _handler.Json(Latest, ReleaseJson("v1.2.0"));
        _handler.Text("https://github.com/yzhoholiev/wslc-desktop/releases/download/v1.2.0/SHA256SUMS.txt", "abc  WslcDesktop-win-x64.exe\n");
        GitHubReleaseFeed feed = Feed();
        AppRelease release = (await feed.FindLatestAsync(false, TestContext.Current.CancellationToken))!;

        Assert.Equal("abc  WslcDesktop-win-x64.exe\n", await feed.ReadChecksumsAsync(release, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadAsync_FollowsARedirectToGitHubsFileHost()
    {
        _handler.Redirect(Asset, "https://release-assets.githubusercontent.com/file?token=1");
        _handler.Text("https://release-assets.githubusercontent.com/file?token=1", "hello");
        List<double> progress = [];

        await Feed().DownloadAsync(Release(5), DownloadPath, new Recorder(progress), TestContext.Current.CancellationToken);

        Assert.Equal("hello", File.ReadAllText(DownloadPath));
        Assert.Equal(1, progress[^1]);
    }

    [Fact]
    public async Task DownloadAsync_RefusesARedirectElsewhere()
    {
        _handler.Redirect(Asset, "https://example.com/x");
        _handler.Text("https://example.com/x", "hello");

        await Assert.ThrowsAsync<UpdateException>(() => Feed().DownloadAsync(Release(5), DownloadPath, new Recorder([]), TestContext.Current.CancellationToken));

        Assert.DoesNotContain(_handler.Requests, request => request.Url.Host == "example.com");
        Assert.False(File.Exists(DownloadPath));
    }

    [Fact]
    public async Task DownloadAsync_RefusesMoreThanFiveRedirects()
    {
        for (int hop = 0; hop < 7; hop++)
        {
            _handler.Redirect(hop == 0 ? Asset : $"https://github.com/hop{hop}", $"https://github.com/hop{hop + 1}");
        }

        await Assert.ThrowsAsync<UpdateException>(() => Feed().DownloadAsync(Release(5), DownloadPath, new Recorder([]), TestContext.Current.CancellationToken));

        Assert.Equal(6, _handler.Requests.Count);
    }

    [Fact]
    public async Task DownloadAsync_StopsWhenTheBodyIsLargerThanAnnounced()
    {
        _handler.Text(Asset, "hello world");

        await Assert.ThrowsAsync<UpdateException>(() => Feed().DownloadAsync(Release(5), DownloadPath, new Recorder([]), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadAsync_DoesNotOverwriteAnExistingFile()
    {
        _handler.Text(Asset, "hello");
        File.WriteAllText(DownloadPath, "mine");

        await Assert.ThrowsAsync<UpdateException>(() => Feed().DownloadAsync(Release(5), DownloadPath, new Recorder([]), TestContext.Current.CancellationToken));

        Assert.Equal("mine", File.ReadAllText(DownloadPath));
    }

    private GitHubReleaseFeed Feed(string assetName = "WslcDesktop-win-x64.exe")
    {
        Assert.True(AppVersion.TryParse("1.1.1-10+Branch.main", out AppVersion? version));
        return new GitHubReleaseFeed(_handler, assetName, version);
    }

    private static AppRelease Release(long size)
    {
        Assert.True(AppVersion.TryParse("1.2.0", out AppVersion? version));
        return new AppRelease(version, AppUpdater.ReleasesPage, new Uri(Asset), size, null);
    }

    private static string ReleaseJson(string tag, bool draft = false) =>
        $$"""
          {
            "tag_name": "{{tag}}",
            "html_url": "https://github.com/yzhoholiev/wslc-desktop/releases/tag/{{tag}}",
            "draft": {{(draft ? "true" : "false")}},
            "prerelease": false,
            "unknown_field": { "nested": [1, 2] },
            "assets": [
              { "name": "WslcDesktop-win-x64.exe", "size": 5, "browser_download_url": "{{Asset}}" },
              { "name": "SHA256SUMS.txt", "size": 90, "browser_download_url": "https://github.com/yzhoholiev/wslc-desktop/releases/download/v1.2.0/SHA256SUMS.txt" }
            ]
          }
          """;

    private sealed class Recorder(List<double> values) : IProgress<double>
    {
        public void Report(double value) => values.Add(value);
    }

    /// <summary>
    ///     Answers each URL with what a test set for it, and records what was asked.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> _answers = [];

        public List<(HttpMethod Method, Uri Url, string[] Headers, bool HasBody)> Requests { get; } = [];

        public void Json(string url, string body) => Text(url, body);

        public void Text(string url, string body) =>
            _answers[url] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8) };

        public void Respond(string url, HttpStatusCode status) => _answers[url] = () => new HttpResponseMessage(status);

        public void Redirect(string url, string location) =>
            _answers[url] = () => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri(location) } };

        public void Fail(string url, Exception failure) => _answers[url] = () => throw failure;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string[] headers = [.. request.Headers.Select(header => $"{header.Key}: {string.Join(", ", header.Value)}").Order()];
            Requests.Add((request.Method, request.RequestUri!, headers, request.Content is not null));
            return Task.FromResult(_answers[request.RequestUri!.AbsoluteUri]());
        }
    }
}
