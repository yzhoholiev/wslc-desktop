namespace WslcDesktop.Services;

/// <summary>
///     The read-only commands both readers run inside the session for VM usage.
/// </summary>
internal static class SessionCommands
{
    public static readonly string[] VmStatus = ["cat", "/proc/loadavg", "/proc/uptime", "/proc/meminfo", "/proc/stat"];

    public static readonly string[] StorageUsage = ["df", "-B1", "/var/lib/docker"];
}
