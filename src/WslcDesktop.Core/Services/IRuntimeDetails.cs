using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     The reads that run as a wslc process of their own: runtime info, container logs, inspect output shown as text, and volume sizes.
/// </summary>
public interface IRuntimeDetails
{
    /// <summary>
    ///     The path of wslc.exe.
    /// </summary>
    string ExecutablePath { get; }

    Task<WslcInfoDto> GetInfoAsync(CancellationToken ct = default);

    Task<string> GetLogsAsync(string id, int tail, CancellationToken ct = default);

    Task<string> InspectAsync(string id, CancellationToken ct = default);

    Task<string> InspectImageAsync(string image, CancellationToken ct = default);

    Task<string> InspectVolumeAsync(string name, CancellationToken ct = default);

    Task<string> InspectNetworkAsync(string name, CancellationToken ct = default);

    /// <summary>
    ///     Size in bytes of each volume the engine can measure, by volume name.
    /// </summary>
    Task<IReadOnlyDictionary<string, long>> ReadVolumeSizesAsync(CancellationToken ct = default);
}
