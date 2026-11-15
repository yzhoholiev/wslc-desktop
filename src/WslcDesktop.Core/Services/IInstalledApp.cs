namespace WslcDesktop.Services;

/// <summary>
///     The running app and the launcher file it was started from. Failures are reported as <see cref="UpdateException" />.
/// </summary>
public interface IInstalledApp
{
    AppVersion RunningVersion { get; }

    /// <summary>
    ///     The release file for this machine's architecture.
    /// </summary>
    string AssetName { get; }

    /// <summary>
    ///     False when the app was started directly, as from a build output, and so has nothing to replace.
    /// </summary>
    bool HasLauncher { get; }

    /// <summary>
    ///     The version of the launcher file as it is now, or null when it is gone or carries no version.
    /// </summary>
    AppVersion? ReadLauncherVersion();

    /// <summary>
    ///     A path beside the launcher that a download can be written to.
    /// </summary>
    string ReserveDownloadPath();

    Task<(long Size, string Sha256)> InspectAsync(string path, CancellationToken ct);

    /// <summary>
    ///     Puts the downloaded file in the launcher's place, leaving the old launcher there when that fails.
    /// </summary>
    void Install(string downloadedPath);

    void Discard(string path);

    /// <summary>
    ///     Starts the launcher with the given arguments.
    /// </summary>
    void Restart(string[] arguments);
}
