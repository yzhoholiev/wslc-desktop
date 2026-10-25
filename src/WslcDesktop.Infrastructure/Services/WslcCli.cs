using System.Text.Json;
using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Typed access to the wslc container CLI: every runtime change and the CLI-only reads.
/// </summary>
public sealed class WslcCli : IRuntimeCommands, IRuntimeDetails
{
    private static readonly WslcJsonContext Json = WslcJsonContext.Default;

    /// <summary>
    ///     Time limit for queries, so a hung VM or engine cannot block the operations waiting on it.
    /// </summary>
    internal static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Time limit for operations on one container, image, volume or network.
    /// </summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    ///     Time limit for starting the runtime, which boots the VM.
    /// </summary>
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(3);

    /// <summary>
    ///     Time limit for prunes and for stopping many containers, which take longer the more there is.
    /// </summary>
    private static readonly TimeSpan BulkTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    ///     Time limit for pulls, which can be large images over a slow connection.
    /// </summary>
    private static readonly TimeSpan PullTimeout = TimeSpan.FromMinutes(30);

    internal WslcCli()
    {
    }

    /// <summary>
    ///     The installed wslc, or the bare name when it is missing, looked up on each use so that installing WSL takes effect without a restart.
    /// </summary>
    public static string ExecutablePath => ResolveExecutable();

    public Task StartContainerAsync(string id) => RunAsync(OperationTimeout, ["start", id]);

    public Task StopContainerAsync(string id) => RunAsync(OperationTimeout, ["stop", id]);

    public Task StopContainersAsync(IReadOnlyCollection<string> ids) =>
        ids.Count == 0 ? Task.CompletedTask : RunAsync(BulkTimeout, ["stop", .. ids]);

    public Task RestartContainerAsync(string id) => RunAsync(OperationTimeout, ["restart", id]);

    public Task RemoveContainerAsync(string id) => RunAsync(OperationTimeout, ["remove", "--force", id]);

    public Task<string> PruneContainersAsync() => RunAsync(BulkTimeout, ["container", "prune", "--force"]);

    public async Task<string> CreateContainerAsync(ContainerRunOptions options, bool start) =>
        (await RunAsync(OperationTimeout, CreateArguments(options, start))).Trim().Split('\n')[^1].Trim();

    public Task PullImageAsync(string image) => RunAsync(PullTimeout, ["pull", "--quiet", image]);

    public Task RemoveImageAsync(string image, bool force) =>
        force
            ? RunAsync(OperationTimeout, ["rmi", "--force", image])
            : RunAsync(OperationTimeout, ["rmi", image]);

    public Task<string> PruneImagesAsync(bool all) =>
        all
            ? RunAsync(BulkTimeout, ["image", "prune", "--all", "--force"])
            : RunAsync(BulkTimeout, ["image", "prune", "--force"]);

    public Task RemoveVolumeAsync(string name) => RunAsync(OperationTimeout, ["volume", "remove", name]);

    public Task<string> PruneVolumesAsync(bool all) =>
        all
            ? RunAsync(BulkTimeout, ["volume", "prune", "--all", "--force"])
            : RunAsync(BulkTimeout, ["volume", "prune", "--force"]);

    public Task RemoveNetworkAsync(string name) => RunAsync(OperationTimeout, ["network", "remove", name]);

    public Task<string> PruneNetworksAsync() => RunAsync(BulkTimeout, ["network", "prune", "--force"]);

    public Task TerminateSessionAsync(string session, TimeSpan? timeout = null) =>
        RunAsync(timeout ?? OperationTimeout, ["--session", session, "system", "session", "terminate"]);

    /// <summary>
    ///     Starts the default session, which wslc creates on demand for any container command.
    /// </summary>
    public Task StartDefaultSessionAsync() => RunAsync(StartTimeout, ["list", "--quiet"]);

    public Task OpenSettingsAsync() => RunAsync(OperationTimeout, ["settings"]);

    string IRuntimeDetails.ExecutablePath => ExecutablePath;

    public async Task<WslcInfoDto> GetInfoAsync(CancellationToken ct = default)
    {
        string output = await RunAsync(QueryTimeout, ["info", "--format", "json"], ct);
        return JsonSerializer.Deserialize(output.Trim(), Json.WslcInfoDto) ?? new WslcInfoDto();
    }

    public Task<string> GetLogsAsync(string id, int tail, CancellationToken ct = default) =>
        RunAsync(QueryTimeout, ["logs", "--tail", tail.ToString(), "--timestamps", id], ct);

    public Task<string> InspectAsync(string id, CancellationToken ct = default) => RunAsync(QueryTimeout, ["inspect", id], ct);

    public Task<string> InspectImageAsync(string image, CancellationToken ct = default) =>
        RunAsync(QueryTimeout, ["image", "inspect", image], ct);

    public Task<string> InspectVolumeAsync(string name, CancellationToken ct = default) =>
        RunAsync(QueryTimeout, ["volume", "inspect", name], ct);

    public Task<string> InspectNetworkAsync(string name, CancellationToken ct = default) =>
        RunAsync(QueryTimeout, ["network", "inspect", name], ct);

    /// <summary>
    ///     The wslc run or create arguments for the options.
    /// </summary>
    /// <param name="options">The container to create.</param>
    /// <param name="start">Whether to run the container detached instead of only creating it.</param>
    /// <exception cref="FormatException">The command has a quote that is not closed.</exception>
    public static string[] CreateArguments(ContainerRunOptions options, bool start)
    {
        List<string> arguments = start ? ["run", "--detach"] : ["create"];

        AddValue("--name", options.Name);
        foreach (PortMapping port in options.Ports.Where(port => !string.IsNullOrWhiteSpace(port.HostPort) && !string.IsNullOrWhiteSpace(port.ContainerPort)))
        {
            arguments.AddRange("--publish", $"{port.HostPort.Trim()}:{port.ContainerPort.Trim()}");
        }

        if (options.PublishAllPorts)
        {
            arguments.Add("--publish-all");
        }

        foreach (VolumeMapping volume in
                 options.Volumes.Where(volume => !string.IsNullOrWhiteSpace(volume.Source) && !string.IsNullOrWhiteSpace(volume.Target)))
        {
            arguments.AddRange("--volume", $"{volume.Source.Trim()}:{volume.Target.Trim()}");
        }

        foreach (KeyValueEntry variable in options.Environment.Where(variable => !string.IsNullOrWhiteSpace(variable.Key)))
        {
            arguments.AddRange("--env", $"{variable.Key.Trim()}={variable.Value}");
        }

        AddValue("--env-file", options.EnvironmentFile);
        AddValue("--entrypoint", options.Entrypoint);
        AddValue("--workdir", options.WorkingDirectory);
        AddValue("--user", options.User);
        AddValue("--network", options.Network);
        AddValue("--hostname", options.Hostname);
        AddValue("--memory", options.Memory);
        AddValue("--cpus", options.Cpus);
        if (options.AllGpus)
        {
            arguments.AddRange("--gpus", "all");
        }

        if (options.Tty)
        {
            arguments.Add("--tty");
        }

        if (options.Interactive)
        {
            arguments.Add("--interactive");
        }

        if (options.RemoveWhenStopped)
        {
            arguments.Add("--rm");
        }

        arguments.Add(options.Image);
        arguments.AddRange(ShellWords.Split(options.Command));
        return [.. arguments];

        void AddValue(string option, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                arguments.AddRange(option, value.Trim());
            }
        }
    }

    internal static string[] SessionRunArguments(string session, params string[] command) => ["--session", session, "system", "session", "run", .. command];

    internal static Task<string> RunAsync(TimeSpan timeout, string[] arguments, CancellationToken ct = default) =>
        Cli.RunCheckedAsync(ExecutablePath, arguments, timeout: timeout, cancellationToken: ct);

    private static string ResolveExecutable()
    {
        string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WSL", "wslc.exe");
        return File.Exists(installed) ? installed : "wslc.exe";
    }
}
