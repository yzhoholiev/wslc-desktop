namespace WslcDesktop.Services;

/// <summary>
///     The single-file launcher the app was started from, which keeps one path while each version unpacks to its own folder.
/// </summary>
public static class Launcher
{
    /// <summary>
    ///     Set by the launcher; the unpacked app lives in a versioned folder that is deleted on upgrade.
    /// </summary>
    private const string PathVariable = "WSLCDESKTOP_LAUNCHER";

    /// <summary>
    ///     The launcher's path, or null when the app was started directly, as from a build output.
    /// </summary>
    public static string? Path { get; } = Accept(Environment.GetEnvironmentVariable(PathVariable));

    /// <summary>
    ///     What to start for a stable entry point: the launcher, or this exe when there is none.
    /// </summary>
    public static string Executable => Path ?? Environment.ProcessPath!;

    /// <summary>
    ///     The path, when it can be the launcher: startup and relaunch commands embed it in quotes, so a quote would break out.
    /// </summary>
    internal static string? Accept(string? path) =>
        path is { Length: > 0 } && System.IO.Path.IsPathFullyQualified(path) && !path.Contains('"') && File.Exists(path) ? path : null;
}
