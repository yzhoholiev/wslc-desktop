using WslcDesktop.Services;

namespace WslcDesktop.Models;

public sealed record Inventory(
    int Images,
    long ImagesSize,
    int UnusedImages,
    int Volumes,
    int AnonymousVolumes,
    int Networks,
    int CustomNetworks,
    long UnusedImagesSize);

/// <summary>
///     One reading of the runtime, as the monitor publishes it.
/// </summary>
public sealed record RuntimeSnapshot(
    WslcInfoDto? Info,
    IReadOnlyList<ContainerDto> Containers,
    IReadOnlyDictionary<string, ContainerStatsDto> Stats,
    VmMetrics? Vm,
    DiskUsage? Storage,
    Inventory? Inventory,
    string? Error)
{
    public static RuntimeSnapshot Empty { get; } = new(null, [], new Dictionary<string, ContainerStatsDto>(), null, null, null, null);

    /// <summary>
    ///     Whether the session this app's wslc commands use exists.
    /// </summary>
    public bool IsRunning => Info is not null && HasDefaultSession(Info);

    /// <summary>
    ///     Whether wslc answered without an error and the session does not exist.
    /// </summary>
    public bool IsKnownStopped => this is { Info: not null, IsRunning: false, Error: null };

    public int RunningContainers => Containers.Count(container => container.IsRunning);

    /// <summary>
    ///     Whether wslc.exe could not be found at all, as opposed to failing.
    /// </summary>
    public bool IsWslcMissing { get; init; }

    /// <summary>
    ///     Keeps the container list for context, but drops measurements so stale values are not shown again.
    /// </summary>
    public RuntimeSnapshot WithError(string? error) => this with
    {
        Error = error,
        Vm = null,
        Stats = new Dictionary<string, ContainerStatsDto>()
    };

    public static bool HasDefaultSession(WslcInfoDto info) =>
        info.Server.Sessions.Any(session => session.Name == Wslc.DefaultSessionName);
}
