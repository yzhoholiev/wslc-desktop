using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     The reads that only wslc answers: runtime info, container logs, and inspect output shown as text.
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
}
