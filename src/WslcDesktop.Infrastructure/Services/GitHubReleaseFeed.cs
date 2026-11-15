using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Reads the app's releases from GitHub and downloads their files, following redirects only between GitHub hosts.
/// </summary>
internal sealed class GitHubReleaseFeed : IReleaseFeed
{
    private const string Repository = "https://api.github.com/repos/yzhoholiev/wslc-desktop";
    private const string ChecksumsName = "SHA256SUMS.txt";
    private const string ApiMediaType = "application/vnd.github+json";
    private const string FileMediaType = "application/octet-stream";

    /// <summary>
    ///     A release download redirects once, to the host that stores release files; anything much longer is a loop.
    /// </summary>
    private const int MaxRedirects = 5;

    private const int BufferSize = 81920;

    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);

    private readonly string _assetName;
    private readonly HttpClient _client;
    private readonly ProductInfoHeaderValue _userAgent;

    public GitHubReleaseFeed(HttpMessageHandler handler, string assetName, AppVersion runningVersion)
    {
        // Each request sets its own time limit, since a download takes far longer than a lookup.
        _client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        _assetName = assetName;
        _userAgent = new ProductInfoHeaderValue("WslcDesktop", runningVersion.ToString());
    }

    public Task<AppRelease?> FindLatestAsync(bool includePreReleases, CancellationToken ct) =>
        RunAsync(async token =>
        {
            if (includePreReleases)
            {
                List<GitHubReleaseDto> releases = await ReadAsync($"{Repository}/releases?per_page=20", GitHubJsonContext.Default.ListGitHubReleaseDto, token) ?? [];
                return releases.Where(release => !release.IsDraft).Select(Map).OfType<AppRelease>().MaxBy(release => release.Version);
            }

            GitHubReleaseDto? latest = await ReadAsync($"{Repository}/releases/latest", GitHubJsonContext.Default.GitHubReleaseDto, token);
            return latest is null ? null : Map(latest);
        }, ApiTimeout, ct);

    public Task<string> ReadChecksumsAsync(AppRelease release, CancellationToken ct) =>
        RunAsync(async token =>
        {
            Uri url = release.Checksums ?? throw new UpdateException("The release has no checksum file.");
            using HttpResponseMessage response = await SendAsync(url, FileMediaType, token);
            EnsureSuccess(response);
            return await response.Content.ReadAsStringAsync(token);
        }, ApiTimeout, ct);

    public Task DownloadAsync(AppRelease release, string path, IProgress<double> progress, CancellationToken ct) =>
        RunAsync(async token =>
        {
            Uri url = release.Asset ?? throw new UpdateException("The release has no download for this computer.");
            using HttpResponseMessage response = await SendAsync(url, FileMediaType, token);
            EnsureSuccess(response);

            await using Stream body = await response.Content.ReadAsStreamAsync(token);
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, true);
            byte[] buffer = new byte[BufferSize];
            long total = 0;
            int read;
            while ((read = await body.ReadAsync(buffer, token)) > 0)
            {
                total += read;
                if (total > release.AssetSize)
                {
                    throw new UpdateException("The download is larger than the release says.");
                }

                await file.WriteAsync(buffer.AsMemory(0, read), token);
                progress.Report((double)total / release.AssetSize);
            }

            return true;
        }, DownloadTimeout, ct);

    private AppRelease? Map(GitHubReleaseDto release)
    {
        if (!AppVersion.TryParse(release.TagName, out AppVersion? version))
        {
            return null;
        }

        GitHubAssetDto? asset = release.Assets?.FirstOrDefault(candidate => candidate.Name == _assetName);
        GitHubAssetDto? checksums = release.Assets?.FirstOrDefault(candidate => candidate.Name == ChecksumsName);
        Uri? assetUrl = Allowed(asset?.DownloadUrl);
        long assetSize = assetUrl is null ? 0 : asset!.Size;

        return new AppRelease(version, Allowed(release.HtmlUrl) ?? AppUpdater.ReleasesPage, assetUrl, assetSize, Allowed(checksums?.DownloadUrl));
    }

    private static Uri? Allowed(string? url) => Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) && GitHubUrl.IsAllowed(parsed) ? parsed : null;

    /// <summary>
    ///     The answer read as JSON, or null when GitHub has nothing at that address.
    /// </summary>
    private async Task<T?> ReadAsync<T>(string url, JsonTypeInfo<T> type, CancellationToken ct) where T : class
    {
        using HttpResponseMessage response = await SendAsync(new Uri(url), ApiMediaType, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response);
        await using Stream body = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync(body, type, ct) ?? throw new JsonException("The answer is empty.");
    }

    private async Task<HttpResponseMessage> SendAsync(Uri url, string mediaType, CancellationToken ct)
    {
        for (int redirects = 0; ; redirects++)
        {
            if (!GitHubUrl.IsAllowed(url))
            {
                throw new UpdateException("The download address is not on GitHub.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.Add(_userAgent);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));

            HttpResponseMessage response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!IsRedirect(response.StatusCode) || response.Headers.Location is not { } location)
            {
                return response;
            }

            response.Dispose();
            if (redirects == MaxRedirects)
            {
                throw new UpdateException("GitHub redirected the download too many times.");
            }

            url = new Uri(url, location);
        }
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // GitHub answers 403 as well as 429 once an address has used up its unauthenticated requests.
        throw new UpdateException(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
            ? "GitHub is limiting requests. Try again later."
            : $"GitHub answered with an error ({(int)response.StatusCode}).");
    }

    private static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> request, TimeSpan timeout, CancellationToken ct)
    {
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limited.CancelAfter(timeout);
        try
        {
            return await request(limited.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new UpdateException("GitHub did not answer in time.");
        }
        catch (HttpRequestException ex)
        {
            throw new UpdateException("GitHub is not reachable.", ex);
        }
        catch (JsonException ex)
        {
            throw new UpdateException("GitHub answered with something unexpected.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UpdateException("The download was interrupted.", ex);
        }
    }
}
