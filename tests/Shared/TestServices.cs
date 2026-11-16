using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests;

/// <summary>
///     An <see cref="AppServices" /> over in-memory fakes, so tests drive the monitor and see which commands ran, without wslc or a VM.
/// </summary>
internal sealed class TestServices
{
    public TestServices(bool hasLauncher = true)
    {
        Installed.HasLauncher = hasLauncher;
        Services = new AppServices(Reader, Session, Commands, Details, Hosts, Wsl, WslConfig, WslcSettings, Shell, Settings, Releases, Installed, Notifier);
    }

    public FakeRuntimeReader Reader { get; } = new();

    public FakeRuntimeSession Session { get; } = new();

    public FakeRuntimeCommands Commands { get; } = new();

    public FakeRuntimeDetails Details { get; } = new();

    public FakeSessionHosts Hosts { get; } = new();

    public FakeWslPlatform Wsl { get; } = new();

    public FakeWslConfig WslConfig { get; } = new();

    public FakeWslcSettings WslcSettings { get; } = new();

    public FakeShellLauncher Shell { get; } = new();

    public InMemorySettings Settings { get; } = new();

    public FakeReleaseFeed Releases { get; } = new();

    public FakeInstalledApp Installed { get; } = new();

    public Notifier Notifier { get; } = new();

    public AppServices Services { get; }
}

internal sealed class FakeRuntimeReader : IRuntimeReader
{
    public List<ContainerDto> Containers { get; set; } = [];

    public Exception? ContainersFailure { get; set; }

    public Exception? ImagesFailure { get; set; }

    public Exception? VmFailure { get; set; }

    public Exception? StorageFailure { get; set; }

    public int StatsReads { get; private set; }

    public int ImageReads { get; private set; }

    public List<ImageDto> Images { get; set; } = [];

    public List<VolumeDto> Volumes { get; set; } = [];

    public List<NetworkDto> Networks { get; set; } = [];

    public ImageConfigDto ImageConfig { get; set; } = new();

    public string VmStatus { get; set; } = "";

    public string StorageUsage { get; set; } = "";

    public Task<List<ContainerDto>> ListContainersAsync(CancellationToken ct) => Answer(ContainersFailure, Containers);

    public Task<List<ContainerStatsDto>> GetStatsAsync(IReadOnlyList<ContainerDto> containers, CancellationToken ct)
    {
        StatsReads++;
        return Task.FromResult(containers.Where(container => container.IsRunning).Select(container => new ContainerStatsDto { Id = container.Id }).ToList());
    }

    public Task<List<ImageDto>> ListImagesAsync(CancellationToken ct)
    {
        ImageReads++;
        return Answer(ImagesFailure, Images);
    }

    public Task<List<VolumeDto>> ListVolumesAsync(CancellationToken ct) => Task.FromResult(Volumes);

    public Task<List<NetworkDto>> ListNetworksAsync(CancellationToken ct) => Task.FromResult(Networks);

    public Task<ImageConfigDto> GetImageConfigAsync(string image, CancellationToken ct) => Task.FromResult(ImageConfig);

    public Task<string> ReadVmStatusAsync(CancellationToken ct) => Answer(VmFailure, VmStatus);

    public Task<string> ReadStorageUsageAsync(CancellationToken ct) => Answer(StorageFailure, StorageUsage);

    private static Task<T> Answer<T>(Exception? failure, T value) => failure is null ? Task.FromResult(value) : Task.FromException<T>(failure);
}

internal sealed class FakeRuntimeSession : IRuntimeSession
{
    public int Disconnects { get; private set; }
    public bool KeepConnected { get; set; } = true;

    public bool IsConnected { get; set; }

    public string? LastConnectionError { get; set; }

    public RuntimeDataSource Source { get; set; } = RuntimeDataSource.Engine;

    public void Disconnect()
    {
        Disconnects++;
        IsConnected = false;
    }
}

/// <summary>
///     Records each command as "verb argument", and fails the commands listed in <see cref="Failing" />.
/// </summary>
internal sealed class FakeRuntimeCommands : IRuntimeCommands
{
    public List<string> Calls { get; } = [];

    public HashSet<string> Failing { get; } = [];

    public string PruneOutput { get; set; } = "";

    public string CreatedId { get; set; } = "created-id";

    public Exception? TerminateFailure { get; set; }

    /// <summary>
    ///     Keeps every command running until it completes.
    /// </summary>
    public Task Completion { get; set; } = Task.CompletedTask;

    public Task StartContainerAsync(string id) => Record($"start {id}");

    public Task StopContainerAsync(string id) => Record($"stop {id}");

    public Task StopContainersAsync(IReadOnlyCollection<string> ids) => Record($"stop-many {string.Join(',', ids)}");

    public Task RestartContainerAsync(string id) => Record($"restart {id}");

    public Task RemoveContainerAsync(string id) => Record($"remove {id}");

    public async Task<string> CreateContainerAsync(ContainerRunOptions options, bool start)
    {
        await Record($"{(start ? "run" : "create")} {options.Image}");
        return CreatedId;
    }

    public Task PullImageAsync(string image) => Record($"pull {image}");

    public Task RemoveImageAsync(string image, bool force) => Record($"rmi {image}{(force ? " --force" : "")}");

    public Task RemoveVolumeAsync(string name) => Record($"volume-remove {name}");

    public Task RemoveNetworkAsync(string name) => Record($"network-remove {name}");

    public Task<string> PruneContainersAsync() => Prune("prune-containers");

    public Task<string> PruneImagesAsync(bool all) => Prune(all ? "prune-images --all" : "prune-images");

    public Task<string> PruneVolumesAsync(bool all) => Prune(all ? "prune-volumes --all" : "prune-volumes");

    public Task<string> PruneNetworksAsync() => Prune("prune-networks");

    public Task StartDefaultSessionAsync() => Record("session-start");

    public Task TerminateSessionAsync(string session, TimeSpan? timeout = null) =>
        TerminateFailure is { } failure ? Task.FromException(failure) : Record($"session-terminate {session}");

    public Task OpenSettingsAsync() => Record("settings");

    private async Task Record(string call)
    {
        Calls.Add(call);
        await Completion;
        string verb = call.Split(' ')[0];
        if (Failing.Contains(verb))
        {
            throw new CliException($"{verb} failed");
        }
    }

    private async Task<string> Prune(string call)
    {
        await Record(call);
        return PruneOutput;
    }
}

internal sealed class FakeRuntimeDetails : IRuntimeDetails
{
    public WslcInfoDto Info { get; set; } = new();

    /// <summary>
    ///     Answers each info read; by default with <see cref="Info" />.
    /// </summary>
    public Func<WslcInfoDto>? ReadInfo { get; set; }

    public int InfoReads { get; private set; }

    public Func<string, int, CancellationToken, Task<string>> ReadLogs { get; set; } = (_, _, _) => Task.FromResult("");

    public Func<string, CancellationToken, Task<string>> ReadInspect { get; set; } = (id, _) => Task.FromResult($"inspect {id}");

    public string ExecutablePath => @"C:\Program Files\WSL\wslc.exe";

    public Task<WslcInfoDto> GetInfoAsync(CancellationToken ct = default)
    {
        InfoReads++;
        return Task.FromResult(ReadInfo is { } read ? read() : Info);
    }

    public Task<string> GetLogsAsync(string id, int tail, CancellationToken ct = default) => ReadLogs(id, tail, ct);

    public Task<string> InspectAsync(string id, CancellationToken ct = default) => ReadInspect(id, ct);

    public Task<string> InspectImageAsync(string image, CancellationToken ct = default) => Task.FromResult($"inspect image {image}");

    public Task<string> InspectVolumeAsync(string name, CancellationToken ct = default) => Task.FromResult($"inspect volume {name}");

    public Task<string> InspectNetworkAsync(string name, CancellationToken ct = default) => Task.FromResult($"inspect network {name}");

    public Func<CancellationToken, Task<IReadOnlyDictionary<string, long>>> ReadVolumeSizes { get; set; } =
        _ => Task.FromResult<IReadOnlyDictionary<string, long>>(new Dictionary<string, long>());

    public Task<IReadOnlyDictionary<string, long>> ReadVolumeSizesAsync(CancellationToken ct = default) => ReadVolumeSizes(ct);
}

internal sealed class FakeSessionHosts : ISessionHosts
{
    public int[] Ids { get; set; } = [];

    public int Lists { get; private set; }

    public int[] ListProcessIds()
    {
        Lists++;
        return Ids;
    }
}

internal sealed class FakeWslPlatform : IWslPlatform
{
    public List<string> Calls { get; } = [];

    public List<WslDistro> Distros { get; set; } = [];

    public List<KeyValueEntry> VersionInfo { get; set; } = [];

    public string UpdateOutput { get; set; } = "";

    /// <summary>
    ///     Thrown by the distribution and version queries.
    /// </summary>
    public Exception? QueryFailure { get; set; }

    /// <summary>
    ///     Holds the shutdown until completed, to test what happens while one runs.
    /// </summary>
    public TaskCompletionSource? PendingShutdown { get; set; }

    public bool IsSettingsAppInstalled { get; set; }

    public Task<List<WslDistro>> ListDistrosAsync(CancellationToken ct = default) =>
        QueryFailure is { } failure ? Task.FromException<List<WslDistro>>(failure) : Task.FromResult(Distros);

    public Task<List<KeyValueEntry>> GetVersionInfoAsync(CancellationToken ct = default) =>
        QueryFailure is { } failure ? Task.FromException<List<KeyValueEntry>>(failure) : Task.FromResult(VersionInfo);

    public async Task ShutdownAsync()
    {
        await Record("shutdown");
        if (PendingShutdown is { } pending)
        {
            await pending.Task;
        }
    }

    public Task TerminateAsync(string distro) => Record($"terminate {distro}");

    public Task SetDefaultAsync(string distro) => Record($"set-default {distro}");

    public async Task<string> UpdateAsync(bool preRelease)
    {
        await Record(preRelease ? "update --pre-release" : "update");
        return UpdateOutput;
    }

    private Task Record(string call)
    {
        Calls.Add(call);
        return Task.CompletedTask;
    }
}

internal sealed class FakeWslConfig : IWslConfig
{
    public string Text { get; set; } = "";

    public string ReadText() => Text;
}

internal sealed class FakeWslcSettings : IWslcSettings
{
    public string Text { get; set; } = "";
    public string FilePath => @"C:\Users\someone\AppData\Local\wslc\settings.yaml";

    public string ReadText() => Text;
}

internal sealed class FakeShellLauncher : IShellLauncher
{
    public List<string> Opened { get; } = [];

    public void OpenContainerShell(string containerId) => Opened.Add($"container-shell {containerId}");

    public void OpenDistroShell(string distro) => Opened.Add($"distro-shell {distro}");

    public void OpenSessionShell(string session) => Opened.Add($"session-shell {session}");

    public void OpenWslConfig() => Opened.Add("wslconfig");

    public void OpenWslSettings() => Opened.Add("wsl-settings");

    public void OpenUrl(Uri url) => Opened.Add($"url {url}");

    public void OpenDataFolder() => Opened.Add("data-folder");

    public void OpenLicenses() => Opened.Add("licenses");
}

internal sealed class InMemorySettings : ISettingsStore
{
    public int Saves { get; private set; }

    /// <summary>
    ///     Thrown when start-with-Windows is changed, as the registry does when access is denied.
    /// </summary>
    public Exception? StartWithWindowsFailure { get; set; }

    public AppSettings Current { get; } = new();

    public bool IsStartWithWindowsEnabled
    {
        get;
        set => field = StartWithWindowsFailure is { } failure ? throw failure : value;
    }

    /// <summary>
    ///     What the settings file holds, as the app's other instance left it.
    /// </summary>
    public AppSettings? Saved { get; set; }

    public AppSettings? ReadSaved() => Saved;

    public void Save() => Saves++;
}

internal sealed class FakeReleaseFeed : IReleaseFeed
{
    public AppRelease? Latest { get; set; }

    public string Checksums { get; set; } = "";

    public Exception? Failure { get; set; }

    public Exception? DownloadFailure { get; set; }

    /// <summary>
    ///     Whether each lookup asked for pre-releases too.
    /// </summary>
    public List<bool> Requests { get; } = [];

    public List<string> Downloads { get; } = [];

    /// <summary>
    ///     Reported before the download ends, as fractions of the whole.
    /// </summary>
    public double[] Progress { get; set; } = [];

    /// <summary>
    ///     Holds the download until completed, to test what happens while one runs.
    /// </summary>
    public TaskCompletionSource? PendingDownload { get; set; }

    public Task<AppRelease?> FindLatestAsync(bool includePreReleases, CancellationToken ct)
    {
        Requests.Add(includePreReleases);
        return Failure is { } failure ? Task.FromException<AppRelease?>(failure) : Task.FromResult(Latest);
    }

    public Task<string> ReadChecksumsAsync(AppRelease release, CancellationToken ct) =>
        Failure is { } failure ? Task.FromException<string>(failure) : Task.FromResult(Checksums);

    public async Task DownloadAsync(AppRelease release, string path, IProgress<double> progress, CancellationToken ct)
    {
        Downloads.Add(path);
        foreach (double fraction in Progress)
        {
            progress.Report(fraction);
        }

        if (PendingDownload is { } pending)
        {
            await pending.Task.WaitAsync(ct);
        }

        if (DownloadFailure is { } failure)
        {
            throw failure;
        }
    }
}

internal sealed class FakeInstalledApp : IInstalledApp
{
    public const string DownloadPath = @"C:\Apps\WslcDesktop.exe.download";

    public AppVersion? LauncherVersion { get; set; } = Version("1.1.0");

    public long DownloadedSize { get; set; }

    public string DownloadedSha256 { get; set; } = "";

    public Exception? ReserveFailure { get; set; }

    public Exception? InstallFailure { get; set; }

    /// <summary>
    ///     Runs while the launcher is being replaced.
    /// </summary>
    public Action? OnInstall { get; set; }

    public List<string> Installed { get; } = [];

    public List<string> Discarded { get; } = [];

    public List<string[]> Restarts { get; } = [];

    public AppVersion RunningVersion { get; set; } = Version("1.1.0");

    public string AssetName => "WslcDesktop-win-x64.exe";

    public bool HasLauncher { get; set; } = true;

    public AppVersion? ReadLauncherVersion() => LauncherVersion;

    public string ReserveDownloadPath() => ReserveFailure is { } failure ? throw failure : DownloadPath;

    public Task<(long Size, string Sha256)> InspectAsync(string path, CancellationToken ct) => Task.FromResult((DownloadedSize, DownloadedSha256));

    public void Install(string downloadedPath)
    {
        if (InstallFailure is { } failure)
        {
            throw failure;
        }

        OnInstall?.Invoke();
        Installed.Add(downloadedPath);
    }

    public void Discard(string path) => Discarded.Add(path);

    public void Restart(string[] arguments) => Restarts.Add(arguments);

    public static AppVersion Version(string text) => AppVersion.TryParse(text, out AppVersion? version) ? version : throw new FormatException(text);
}

