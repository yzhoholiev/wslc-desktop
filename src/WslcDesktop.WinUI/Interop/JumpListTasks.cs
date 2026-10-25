using WslcDesktop.Services;

namespace WslcDesktop.Interop;

/// <summary>
///     The taskbar jump list's tasks, each starting the launcher with a launch command.
/// </summary>
public static class JumpListTasks
{
    /// <summary>
    ///     The tasks in display order.
    /// </summary>
    public static JumpListItem[] All { get; } =
    [
        Entry("Containers", LaunchCommand.ShowContainers, "containers"),
        Entry("Images", LaunchCommand.ShowImages, "images"),
        Entry("Volumes", LaunchCommand.ShowVolumes, "volumes"),
        Entry("WSL", LaunchCommand.ShowWsl, "wsl"),
        new JumpListSeparator(),
        Entry("Start container runtime", LaunchCommand.StartRuntime, "start"),
        Entry("Stop container runtime…", LaunchCommand.StopRuntime, "stop"),
        Entry("Clean up…", LaunchCommand.Cleanup, "cleanup")
    ];

    private static JumpListTask Entry(string title, LaunchCommand command, string icon) =>
        new(title, Launcher.Executable, LaunchCommands.ArgumentOf(command), IconPath(icon));

    /// <summary>
    ///     The icon file in this unpacked version's Assets\JumpList, which follows upgrades because the list is rewritten at every start.
    /// </summary>
    private static string IconPath(string icon)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "JumpList", $"{icon}.ico");
        return File.Exists(path) ? path : Launcher.Executable;
    }
}
