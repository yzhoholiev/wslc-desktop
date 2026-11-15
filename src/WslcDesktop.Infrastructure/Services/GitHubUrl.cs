namespace WslcDesktop.Services;

/// <summary>
///     Decides which addresses the app may request when it checks for and downloads its updates.
/// </summary>
internal static class GitHubUrl
{
    /// <summary>
    ///     Whether the address is an https one on a GitHub host, without credentials or a port of its own.
    /// </summary>
    public static bool IsAllowed(Uri url) =>
        url.IsAbsoluteUri
        && url.Scheme == Uri.UriSchemeHttps
        && url.IsDefaultPort
        && url.UserInfo.Length == 0
        && (url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || url.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase)
            || url.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));
}
