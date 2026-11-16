using System.Globalization;

namespace WslcDesktop.Services;

/// <summary>
///     The read-only commands run inside the session: VM usage for both readers, and Docker Engine API requests.
/// </summary>
internal static class SessionCommands
{
    public static readonly string[] VmStatus = ["cat", "/proc/loadavg", "/proc/uptime", "/proc/meminfo", "/proc/stat"];

    public static readonly string[] StorageUsage = ["df", "-B1", "/var/lib/docker"];

    /// <summary>
    ///     The curl command for a Docker Engine API request; with --fail a failed request prints no body.
    /// </summary>
    public static string[] EngineRequest(int maxTimeSeconds, string url) =>
        ["curl", "-sS", "--fail", "--max-time", maxTimeSeconds.ToString(CultureInfo.InvariantCulture), "--unix-socket", "/var/run/docker.sock", url];
}
