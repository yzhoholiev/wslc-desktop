using System.Text.Json.Serialization;

namespace WslcDesktop.Models;

internal sealed class EngineContainer
{
    public string Id { get; set; } = "";
    public List<string> Names { get; set; } = [];
    public string Image { get; set; } = "";

    [JsonPropertyName("ImageID")]
    public string ImageId { get; set; } = "";

    public string State { get; set; } = "";
    public string Status { get; set; } = "";
    public List<EnginePort>? Ports { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
    public EngineNetworkSettings? NetworkSettings { get; set; }
    public List<EngineMount>? Mounts { get; set; }
}

internal sealed class EngineMount
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>
///     The JSON wslc stores in the com.microsoft.wsl.container.metadata label of each container it creates.
/// </summary>
internal sealed class WslcContainerMetadata
{
    public WslcContainerMetadataV1? V1 { get; set; }
}

internal sealed class WslcContainerMetadataV1
{
    public List<WslcPortMapping>? Ports { get; set; }
}

internal sealed class WslcPortMapping
{
    public string BindingAddress { get; set; } = "";
    public int ContainerPort { get; set; }
    public int HostPort { get; set; }

    /// <summary>
    ///     IP protocol number: 6 for TCP, 17 for UDP.
    /// </summary>
    public int Protocol { get; set; }
}

internal sealed class EnginePort
{
    [JsonPropertyName("IP")]
    public string? Ip { get; set; }

    public int PrivatePort { get; set; }
    public int? PublicPort { get; set; }
    public string Type { get; set; } = "";
}

internal sealed class EngineNetworkSettings
{
    public Dictionary<string, EngineEndpoint>? Networks { get; set; }
}

internal sealed class EngineEndpoint;

internal sealed class EngineImage
{
    public string Id { get; set; } = "";
    public List<string>? RepoTags { get; set; }
    public long Created { get; set; }
    public long Size { get; set; }
}

internal sealed class EngineVolumeList
{
    public List<EngineVolume>? Volumes { get; set; }
}

internal sealed class EngineVolume
{
    public string Name { get; set; } = "";
    public string Driver { get; set; } = "";
    public string Mountpoint { get; set; } = "";
    public Dictionary<string, string>? Labels { get; set; }
}

internal sealed class EngineNetwork
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Driver { get; set; } = "";
    public bool Internal { get; set; }

    [JsonPropertyName("EnableIPv6")]
    public bool EnableIpv6 { get; set; }

    public string Created { get; set; } = "";
}

internal sealed class EngineStats
{
    [JsonPropertyName("cpu_stats")]
    public EngineCpuStats? CpuStats { get; set; }

    [JsonPropertyName("memory_stats")]
    public EngineMemoryStats? MemoryStats { get; set; }

    [JsonPropertyName("networks")]
    public Dictionary<string, EngineNetworkStats>? Networks { get; set; }

    [JsonPropertyName("blkio_stats")]
    public EngineBlkioStats? BlkioStats { get; set; }

    [JsonPropertyName("pids_stats")]
    public EnginePidsStats? PidsStats { get; set; }
}

internal sealed class EngineCpuStats
{
    [JsonPropertyName("cpu_usage")]
    public EngineCpuUsage? CpuUsage { get; set; }

    [JsonPropertyName("system_cpu_usage")]
    public ulong SystemCpuUsage { get; set; }

    [JsonPropertyName("online_cpus")]
    public uint OnlineCpus { get; set; }
}

internal sealed class EngineCpuUsage
{
    [JsonPropertyName("total_usage")]
    public ulong TotalUsage { get; set; }
}

internal sealed class EngineMemoryStats
{
    [JsonPropertyName("usage")]
    public ulong Usage { get; set; }

    [JsonPropertyName("limit")]
    public ulong Limit { get; set; }

    [JsonPropertyName("stats")]
    public Dictionary<string, ulong>? Stats { get; set; }
}

internal sealed class EngineNetworkStats
{
    [JsonPropertyName("rx_bytes")]
    public ulong RxBytes { get; set; }

    [JsonPropertyName("tx_bytes")]
    public ulong TxBytes { get; set; }
}

internal sealed class EngineBlkioStats
{
    [JsonPropertyName("io_service_bytes_recursive")]
    public List<EngineBlkioEntry>? IoServiceBytesRecursive { get; set; }
}

internal sealed class EngineBlkioEntry
{
    [JsonPropertyName("op")]
    public string Op { get; set; } = "";

    [JsonPropertyName("value")]
    public ulong Value { get; set; }
}

internal sealed class EnginePidsStats
{
    [JsonPropertyName("current")]
    public ulong Current { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(List<EngineContainer>))]
[JsonSerializable(typeof(List<EngineImage>))]
[JsonSerializable(typeof(EngineVolumeList))]
[JsonSerializable(typeof(List<EngineNetwork>))]
[JsonSerializable(typeof(EngineStats))]
[JsonSerializable(typeof(ImageInspectDto))]
[JsonSerializable(typeof(WslcContainerMetadata))]
internal sealed partial class DockerEngineJsonContext : JsonSerializerContext;
