namespace WslcDesktop.Services;

/// <summary>
///     A user-started operation and the text its notifications show.
/// </summary>
public sealed record UserOperation(string Description, Func<Task> Run, string? SuccessMessage = null);

/// <summary>
///     The operations on one container, worded the same wherever they are started.
/// </summary>
public sealed class ContainerActions(IRuntimeCommands commands)
{
    public UserOperation Start(string id, string name) => new($"Starting {name}", () => commands.StartContainerAsync(id), $"Started {name}.");

    public UserOperation Stop(string id, string name) => new($"Stopping {name}", () => commands.StopContainerAsync(id), $"Stopped {name}.");

    public UserOperation StartOrStop(string id, string name, bool isRunning) => isRunning ? Stop(id, name) : Start(id, name);

    public UserOperation Restart(string id, string name) => new($"Restarting {name}", () => commands.RestartContainerAsync(id), $"Restarted {name}.");

    public UserOperation Remove(string id, string name) => new($"Removing {name}", () => commands.RemoveContainerAsync(id), $"Removed {name}.");
}
