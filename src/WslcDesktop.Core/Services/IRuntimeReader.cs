using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     One way of reading the runtime's containers, stats, inventory and VM usage.
/// </summary>
public interface IRuntimeReader
{
    Task<List<ContainerDto>> ListContainersAsync(CancellationToken ct);

    Task<List<ContainerStatsDto>> GetStatsAsync(IReadOnlyList<ContainerDto> containers, CancellationToken ct);

    Task<List<ImageDto>> ListImagesAsync(CancellationToken ct);

    Task<List<VolumeDto>> ListVolumesAsync(CancellationToken ct);

    Task<List<NetworkDto>> ListNetworksAsync(CancellationToken ct);

    Task<ImageConfigDto> GetImageConfigAsync(string image, CancellationToken ct);

    /// <summary>
    ///     The session VM's /proc/loadavg, /proc/uptime, /proc/meminfo and /proc/stat, in that order.
    /// </summary>
    Task<string> ReadVmStatusAsync(CancellationToken ct);

    /// <summary>
    ///     <c>df -B1</c> output for the file system that holds the containers.
    /// </summary>
    Task<string> ReadStorageUsageAsync(CancellationToken ct);
}
