namespace WslcDesktop.Services;

/// <summary>
///     What the app knows about wslc without running it.
/// </summary>
public static class Wslc
{
    public const string NotInstalledMessage =
        "wslc.exe was not found. It ships with WSL 2.9.3 and later, so update WSL to get it, or install WSL if it is missing.";

    public static readonly Uri DocumentationUrl = new("https://learn.microsoft.com/windows/wsl/wsl-container");

    /// <summary>
    ///     The session wslc commands use when none is named.
    /// </summary>
    public static string DefaultSessionName { get; } =
        // Observed with wslc 3.0.3: elevated and non-elevated processes of one user get separate sessions, i.e. separate VMs.
        (Elevation.IsElevated ? "wslc-cli-admin-" : "wslc-cli-") + Environment.UserName;
}
