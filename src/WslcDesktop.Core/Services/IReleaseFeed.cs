using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Where the app's releases are published. Failures are reported as <see cref="UpdateException" />.
/// </summary>
public interface IReleaseFeed
{
    /// <summary>
    ///     The newest release, or null when there is none that can be read.
    /// </summary>
    Task<AppRelease?> FindLatestAsync(bool includePreReleases, CancellationToken ct);

    Task<string> ReadChecksumsAsync(AppRelease release, CancellationToken ct);

    /// <summary>
    ///     Saves the release's launcher to a new file, reporting the downloaded fraction from 0 to 1.
    /// </summary>
    Task DownloadAsync(AppRelease release, string path, IProgress<double> progress, CancellationToken ct);
}
