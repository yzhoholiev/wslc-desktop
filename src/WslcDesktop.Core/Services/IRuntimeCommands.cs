using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Every change to the runtime, which goes through wslc: it maps Windows paths into bind mounts and publishes ports on localhost.
/// </summary>
public interface IRuntimeCommands
{
    Task StartContainerAsync(string id);

    Task StopContainerAsync(string id);

    Task StopContainersAsync(IReadOnlyCollection<string> ids);

    Task RestartContainerAsync(string id);

    Task RemoveContainerAsync(string id);

    /// <summary>
    ///     Creates a container, and starts it in the background when asked; returns the new container's ID.
    /// </summary>
    Task<string> CreateContainerAsync(ContainerRunOptions options, bool start);

    Task PullImageAsync(string image);

    Task RemoveImageAsync(string image, bool force);

    Task RemoveVolumeAsync(string name);

    Task RemoveNetworkAsync(string name);

    Task<string> PruneContainersAsync();

    Task<string> PruneImagesAsync(bool all);

    Task<string> PruneVolumesAsync(bool all);

    Task<string> PruneNetworksAsync();

    /// <summary>
    ///     Starts the default session, which boots its VM.
    /// </summary>
    Task StartDefaultSessionAsync();

    Task TerminateSessionAsync(string session, TimeSpan? timeout = null);

    /// <summary>
    ///     Opens wslc's settings file in the default editor.
    /// </summary>
    Task OpenSettingsAsync();
}
