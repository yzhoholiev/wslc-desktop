using System.Collections.Frozen;

namespace WslcDesktop.Services;

/// <summary>
///     What a launch asks the app to do, from its command line.
/// </summary>
public enum LaunchCommand
{
    None,
    ShowContainers,
    ShowImages,
    ShowVolumes,
    ShowWsl,
    StartRuntime,
    StopRuntime,
    Cleanup
}

/// <summary>
///     Maps launch commands to and from their command-line arguments.
/// </summary>
public static class LaunchCommands
{
    /// <summary>
    ///     Starts the app hidden in the notification area, as sign-in does.
    /// </summary>
    public const string TrayArgument = "--tray";

    private static readonly FrozenDictionary<string, LaunchCommand> ByArgument = FrozenDictionary.Create(
        StringComparer.OrdinalIgnoreCase,
        KeyValuePair.Create("--containers", LaunchCommand.ShowContainers),
        KeyValuePair.Create("--images", LaunchCommand.ShowImages),
        KeyValuePair.Create("--volumes", LaunchCommand.ShowVolumes),
        KeyValuePair.Create("--wsl", LaunchCommand.ShowWsl),
        KeyValuePair.Create("--start-runtime", LaunchCommand.StartRuntime),
        KeyValuePair.Create("--stop-runtime", LaunchCommand.StopRuntime),
        KeyValuePair.Create("--cleanup", LaunchCommand.Cleanup));

    /// <summary>
    ///     The first command the arguments name, or <see cref="LaunchCommand.None" />.
    /// </summary>
    public static LaunchCommand Parse(IEnumerable<string> arguments) =>
        arguments.Select(argument => ByArgument.GetValueOrDefault(argument)).FirstOrDefault(command => command != LaunchCommand.None);

    public static string ArgumentOf(LaunchCommand command) => ByArgument.First(entry => entry.Value == command).Key;
}
