namespace WslcDesktop.Controls;

/// <summary>
///     The help bubble of each run dialog option.
/// </summary>
internal static class RunImageHelp
{
    public const string Name =
        "How wslc commands and this app refer to the container. Letters, digits, '.', '_' and '-'; left blank, a name is generated.";

    public const string Ports =
        "Makes a port the container listens on reachable from Windows at localhost and the host port. "
        + "The container side is the port and protocol, such as 80/tcp.";

    public const string Volumes =
        "Mounts a Windows folder, or a named volume that keeps data between containers, at a path inside the container. "
        + "What is written there outlives the container.";

    public const string Environment =
        "Values the container's programs read from their environment, often settings such as passwords or ports. "
        + "The image's own variables still apply unless you set the same name.";

    public const string Command =
        "What the container runs, in place of the image's default command. Quote arguments the way a Linux shell does, e.g. sh -c 'echo hi'.";

    public const string Entrypoint = "The program that runs the command, which becomes its arguments. Rarely needs changing.";

    public const string WorkingDirectory = "The folder inside the container that the command starts in.";

    public const string User = "The Linux user the command runs as, by name or as uid:gid. Most images run as root.";

    public const string Network = "The network the container joins. Containers on the same network you created reach each other by name.";

    public const string Hostname = "The name the container sees as its own host name.";

    public const string Memory = "The most memory the container may use; its processes are killed when they go over. Blank means no limit.";

    public const string Cpus = "How many CPU cores' worth of time the container may use, such as 0.5 or 2. Blank means no limit.";

    public const string EnvironmentFile = "A Windows file with one KEY=value per line, read when the container is created.";

    public const string PublishAll = "Also publishes every port the image declares to a random free port on Windows; the container's row shows which.";

    public const string Tty =
        "Gives the command a terminal, as if someone sat at a console: prompts and colours. "
        + "Together with open input it keeps a shell such as alpine's running; servers do not need it.";

    public const string Interactive =
        "Keeps the command's input open. A shell such as alpine's exits at once without it, since its input is empty; servers do not read input.";

    public const string RemoveWhenStopped = "Deletes the container, and its logs, as soon as it stops. Its named volumes are kept.";

    public const string AllGpus = "Passes every GPU of this PC through to the container, for CUDA and other GPU work.";
}
