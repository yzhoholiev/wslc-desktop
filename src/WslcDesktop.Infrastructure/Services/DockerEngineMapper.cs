using System.Globalization;
using System.Text.Json;
using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Converts Docker Engine API responses into the DTOs that wslc's own JSON output deserializes to.
/// </summary>
internal static class DockerEngineMapper
{
    private const string WslcMetadataLabel = "com.microsoft.wsl.container.metadata";
    private const int UdpProtocol = 17;

    private static readonly DockerEngineJsonContext Json = DockerEngineJsonContext.Default;

    public static ContainerDto ToContainerDto(EngineContainer container) => new()
    {
        Id = container.Id,
        Names = container.Names.FirstOrDefault()?.TrimStart('/') ?? Humanize.ShortId(container.Id),
        Image = container.Image,
        State = container.State,
        Status = container.Status,
        Ports = FormatPorts(container),
        Networks = string.Join(",", container.NetworkSettings?.Networks?.Keys ?? Enumerable.Empty<string>()),
        Mounts = string.Join(",", (container.Mounts ?? []).Where(mount => mount.Type == "volume").Select(mount => mount.Name)),
        ImageId = container.ImageId
    };

    public static IEnumerable<ImageDto> ToImageDtos(EngineImage image, int containers)
    {
        List<string> tags = image.RepoTags is { Count: > 0 } repoTags ? repoTags : ["<none>:<none>"];
        var created = DateTimeOffset.FromUnixTimeSeconds(image.Created);

        foreach (string repoTag in tags)
        {
            int separator = repoTag.LastIndexOf(':');
            yield return new ImageDto
            {
                Id = image.Id,
                Repository = separator > 0 ? repoTag[..separator] : repoTag,
                Tag = separator > 0 ? repoTag[(separator + 1)..] : "<none>",
                Size = ByteSize.Format(image.Size),
                CreatedAt = created.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                CreatedSince = Humanize.Ago(created),
                Containers = containers.ToString(CultureInfo.InvariantCulture)
            };
        }
    }

    public static VolumeDto ToVolumeDto(EngineVolume volume) => new()
    {
        Name = volume.Name,
        Driver = volume.Driver,
        Mountpoint = volume.Mountpoint,
        Labels = string.Join(",", (volume.Labels ?? []).Select(label => $"{label.Key}={label.Value}"))
    };

    public static NetworkDto ToNetworkDto(EngineNetwork network) => new()
    {
        Id = network.Id,
        Name = network.Name,
        Driver = network.Driver,
        Internal = network.Internal ? "true" : "false",
        IPv6 = network.EnableIpv6 ? "true" : "false",
        CreatedAt = network.Created.Replace('T', ' ')
    };

    public static ContainerStatsDto ToStatsDto(string id, EngineStats stats, double cpuPercent)
    {
        EngineMemoryStats? memory = stats.MemoryStats;

        // Like the Docker CLI, exclude reclaimable page cache from used memory (cgroup v2 reports it as inactive_file).
        ulong cache = memory?.Stats?.GetValueOrDefault("inactive_file") ?? memory?.Stats?.GetValueOrDefault("total_inactive_file") ?? 0;
        ulong used = memory is null ? 0 : memory.Usage - Math.Min(cache, memory.Usage);
        ulong limit = memory?.Limit ?? 0;

        ulong rx = stats.Networks?.Values.Aggregate(0UL, (sum, network) => sum + network.RxBytes) ?? 0;
        ulong tx = stats.Networks?.Values.Aggregate(0UL, (sum, network) => sum + network.TxBytes) ?? 0;
        List<EngineBlkioEntry> blkio = stats.BlkioStats?.IoServiceBytesRecursive ?? [];
        ulong read = blkio.Where(entry => entry.Op.Equals("read", StringComparison.OrdinalIgnoreCase)).Aggregate(0UL, (sum, entry) => sum + entry.Value);
        ulong written = blkio.Where(entry => entry.Op.Equals("write", StringComparison.OrdinalIgnoreCase)).Aggregate(0UL, (sum, entry) => sum + entry.Value);

        return new ContainerStatsDto
        {
            Id = id,
            CpuPerc = FormattableString.Invariant($"{cpuPercent:0.00}%"),
            MemPerc = FormattableString.Invariant($"{(limit == 0 ? 0 : 100d * used / limit):0.00}%"),
            MemUsage = $"{ByteSize.Format((long)used)} / {ByteSize.Format((long)limit)}",
            NetIO = $"{ByteSize.Format((long)rx)} / {ByteSize.Format((long)tx)}",
            BlockIO = $"{ByteSize.Format((long)read)} / {ByteSize.Format((long)written)}",
            Pids = (int)(stats.PidsStats?.Current ?? 0)
        };
    }

    /// <summary>
    ///     Formats published ports as the CLI does, e.g. "127.0.0.1:8080->80/tcp".
    /// </summary>
    private static string FormatPorts(EngineContainer container)
    {
        // The engine only sees the VM-side relay port; the Windows-side binding exists only in wslc's own metadata.
        if (container.Labels?.GetValueOrDefault(WslcMetadataLabel) is { } metadataJson)
        {
            List<WslcPortMapping> mappings = TryParseMetadata(metadataJson)?.V1?.Ports ?? [];
            return string.Join(", ", mappings.Select(port =>
                $"{port.BindingAddress}:{port.HostPort}->{port.ContainerPort}/{(port.Protocol == UdpProtocol ? "udp" : "tcp")}"));
        }

        return string.Join(", ", (container.Ports ?? [])
            .Select(port => port.PublicPort is { } published
                ? $"{port.Ip}:{published}->{port.PrivatePort}/{port.Type}"
                : $"{port.PrivatePort}/{port.Type}")
            .Distinct());
    }

    private static WslcContainerMetadata? TryParseMetadata(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, Json.WslcContainerMetadata);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
