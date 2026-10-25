using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     WSL itself: its distributions, version, updates and shutdown.
/// </summary>
public interface IWslPlatform
{
    bool IsSettingsAppInstalled { get; }

    Task<List<WslDistro>> ListDistrosAsync(CancellationToken ct = default);

    Task<List<KeyValueEntry>> GetVersionInfoAsync(CancellationToken ct = default);

    Task ShutdownAsync();

    Task TerminateAsync(string distro);

    Task SetDefaultAsync(string distro);

    /// <summary>
    ///     Updates WSL and returns what wsl.exe printed.
    /// </summary>
    Task<string> UpdateAsync(bool preRelease);
}
