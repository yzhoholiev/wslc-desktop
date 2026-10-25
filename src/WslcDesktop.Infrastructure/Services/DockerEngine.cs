using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Raised when the session is up but its Docker Engine API cannot be used, so callers fall back to the wslc CLI.
/// </summary>
internal sealed class EngineUnavailableException(string message) : CliException(message);

/// <summary>
///     Reads the runtime through the Docker Engine API inside the wslc session.
/// </summary>
internal sealed class DockerEngine(ISessionShell shell) : IRuntimeConnection
{
    /// <summary>
    ///     v1.41 is the oldest API with one-shot stats; pinning it keeps response shapes stable across engine upgrades.
    /// </summary>
    private const string Curl = "curl -sS --fail --max-time 10 --unix-socket /var/run/docker.sock http://localhost/v1.41";

    private const string StatsIdPrefix = "@id ";

    private static readonly DockerEngineJsonContext Json = DockerEngineJsonContext.Default;

    private readonly Dictionary<string, (ulong Container, ulong System)> _previousCpu = [];

    public bool IsConnected => shell.IsConnected;

    public async Task<List<ContainerDto>> ListContainersAsync(CancellationToken ct)
    {
        List<EngineContainer> containers = Deserialize(await GetAsync("/containers/json?all=1", ct), Json.ListEngineContainer);
        return [.. containers.Select(DockerEngineMapper.ToContainerDto)];
    }

    public async Task<List<ContainerStatsDto>> GetStatsAsync(IReadOnlyList<ContainerDto> containers, CancellationToken ct)
    {
        var ids = containers.Where(container => container.IsRunning).Select(container => container.Id).ToList();
        if (ids.Count == 0)
        {
            _previousCpu.Clear();
            return [];
        }

        // One-shot responses carry no id or name, so each is preceded by its id.
        string script = string.Join("; ", ids.Select(id =>
            $"echo '{StatsIdPrefix}{id}'; {Curl}'/containers/{id}/stats?stream=false&one-shot=true' 2>&1; echo"));

        ShellResult result = await shell.ExecuteAsync(script, ct);
        EnsureSucceeded(result, "stats");

        var stats = new List<ContainerStatsDto>();
        string? currentId = null;
        foreach (string line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith(StatsIdPrefix, StringComparison.Ordinal))
            {
                currentId = line[StatsIdPrefix.Length..];
            }
            else if (currentId is not null && line.StartsWith('{') && TryParseStats(line) is { } sample)
            {
                stats.Add(DockerEngineMapper.ToStatsDto(currentId, sample, CpuPercent(currentId, sample)));
                currentId = null;
            }
        }

        foreach (string stale in _previousCpu.Keys.Except(ids).ToList())
        {
            _previousCpu.Remove(stale);
        }

        return stats;
    }

    public async Task<List<ImageDto>> ListImagesAsync(CancellationToken ct)
    {
        List<EngineImage> images = Deserialize(await GetAsync("/images/json", ct), Json.ListEngineImage);

        // The engine reports per-image container counts as -1 unless asked for disk usage, so count by image ID instead.
        var usage = Deserialize(await GetAsync("/containers/json?all=1", ct), Json.ListEngineContainer)
            .GroupBy(container => container.ImageId)
            .ToDictionary(group => group.Key, group => group.Count());

        return [.. images.SelectMany(image => DockerEngineMapper.ToImageDtos(image, usage.GetValueOrDefault(image.Id)))];
    }

    public async Task<List<VolumeDto>> ListVolumesAsync(CancellationToken ct) =>
    [
        .. (Deserialize(await GetAsync("/volumes", ct), Json.EngineVolumeList).Volumes ?? []).Select(DockerEngineMapper.ToVolumeDto)
    ];

    public async Task<List<NetworkDto>> ListNetworksAsync(CancellationToken ct) =>
    [
        .. Deserialize(await GetAsync("/networks", ct), Json.ListEngineNetwork).Select(DockerEngineMapper.ToNetworkDto)
    ];

    public async Task<ImageConfigDto> GetImageConfigAsync(string image, CancellationToken ct)
    {
        // The reference becomes part of a shell command line.
        if (!ImageReference.IsValid(image))
        {
            throw new CliException($"'{image}' is not an image reference.");
        }

        string json;
        try
        {
            json = await GetAsync($"/images/{image}/json", ct);
        }
        catch (EngineUnavailableException ex) when (ex.Message.Contains("error: 404", StringComparison.Ordinal))
        {
            // A missing image is an answer, not a broken engine, so it must not switch every read over to the CLI.
            throw new CliException($"Image '{image}' was not found.");
        }

        return Deserialize(json, Json.ImageInspectDto).Config;
    }

    public Task<string> ReadVmStatusAsync(CancellationToken ct) => RunInSessionAsync(SessionCommands.VmStatus, ct);

    public Task<string> ReadStorageUsageAsync(CancellationToken ct) => RunInSessionAsync(SessionCommands.StorageUsage, ct);

    public void Dispose() => shell.Dispose();

    private async Task<string> RunInSessionAsync(string[] command, CancellationToken ct)
    {
        string commandLine = string.Join(' ', command);
        ShellResult result = await shell.ExecuteAsync(commandLine, ct);
        return result.ExitCode == 0
            ? result.Output
            : throw new CliException(result.Output.Trim() is { Length: > 0 } output ? output : $"'{commandLine}' exited with code {result.ExitCode}.");
    }

    private async Task<string> GetAsync(string path, CancellationToken ct)
    {
        // Single quotes keep the shell from globbing '?' and splitting on '&' in query strings.
        // stderr joins the output so a failure carries curl's reason; with --fail a failed request prints no body.
        ShellResult result = await shell.ExecuteAsync($"{Curl}'{path}' 2>&1", ct);
        EnsureSucceeded(result, path);
        return result.Output;
    }

    private static void EnsureSucceeded(ShellResult result, string what)
    {
        if (result.ExitCode != 0)
        {
            throw new EngineUnavailableException($"Docker Engine request for {what} failed ({result.ExitCode}): {result.Output}");
        }
    }

    private static T Deserialize<T>(string json, JsonTypeInfo<T> typeInfo)
    {
        try
        {
            return JsonSerializer.Deserialize(json, typeInfo) ?? throw new EngineUnavailableException("The Docker Engine returned no data.");
        }
        catch (JsonException ex)
        {
            throw new EngineUnavailableException($"Unexpected Docker Engine response: {ex.Message}");
        }
    }

    /// <summary>
    ///     curl --max-time can cut a response short; one bad sample must not drop the stats of every container.
    /// </summary>
    private static EngineStats? TryParseStats(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, Json.EngineStats);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    ///     The Docker CLI's formula, measured against this app's previous one-shot sample because one-shot omits precpu_stats.
    /// </summary>
    private double CpuPercent(string id, EngineStats stats)
    {
        EngineCpuStats? cpu = stats.CpuStats;
        if (cpu?.CpuUsage is null)
        {
            return 0;
        }

        (ulong Container, ulong System) current = (Container: cpu.CpuUsage.TotalUsage, System: cpu.SystemCpuUsage);
        bool hadPrevious = _previousCpu.TryGetValue(id, out (ulong Container, ulong System) previous);
        _previousCpu[id] = current;

        if (!hadPrevious || current.System <= previous.System || current.Container < previous.Container)
        {
            return 0;
        }

        double containerDelta = current.Container - previous.Container;
        double systemDelta = current.System - previous.System;
        return containerDelta / systemDelta * Math.Max(1, cpu.OnlineCpus) * 100;
    }
}
