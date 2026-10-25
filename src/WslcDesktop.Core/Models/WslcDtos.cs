using System.Text.Json;
using System.Text.Json.Serialization;

namespace WslcDesktop.Models;

public sealed class ContainerDto
{
    public string Id { get; set; } = "";
    public string Names { get; set; } = "";
    public string Image { get; set; } = "";
    public string State { get; set; } = "";
    public string Status { get; set; } = "";
    public string Ports { get; set; } = "";
    public string Networks { get; set; } = "";

    /// <summary>
    ///     Comma-separated volume names; the CLI truncates long names with an ellipsis.
    /// </summary>
    public string Mounts { get; set; } = "";

    /// <summary>
    ///     Full image ID, which only the Docker Engine API reports.
    /// </summary>
    [JsonIgnore]
    public string ImageId { get; set; } = "";

    [JsonIgnore]
    public bool IsRunning => State == "running";

    /// <summary>
    ///     Running, paused or restarting: a container that still holds its image, networks and volumes.
    /// </summary>
    [JsonIgnore]
    public bool IsActive => State is "running" or "paused" or "restarting";
}

public sealed class ContainerStatsDto
{
    public string Id { get; set; } = "";
    public string CpuPerc { get; set; } = "";
    public string MemPerc { get; set; } = "";
    public string MemUsage { get; set; } = "";
    public string NetIO { get; set; } = "";
    public string BlockIO { get; set; } = "";
    public int Pids { get; set; }

    /// <summary>
    ///     The used part of <see cref="MemUsage" />, which reads "used / limit".
    /// </summary>
    [JsonIgnore]
    public string MemUsed => MemUsage.Split('/')[0].Trim();
}

public sealed class ImageDto
{
    public string Id { get; set; } = "";
    public string Repository { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Size { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string CreatedSince { get; set; } = "";
    public string Containers { get; set; } = "";
}

public sealed class ImageInspectDto
{
    public ImageConfigDto Config { get; set; } = new();
}

/// <summary>
///     The defaults an image gives its containers.
/// </summary>
public sealed class ImageConfigDto
{
    private static readonly string[] Shells = ["sh", "bash", "ash", "dash", "zsh", "ksh", "fish"];
    public string[]? Cmd { get; set; }
    public string[]? Entrypoint { get; set; }

    /// <summary>
    ///     Keyed by port and protocol, such as "80/tcp".
    /// </summary>
    public Dictionary<string, JsonElement>? ExposedPorts { get; set; }

    /// <summary>
    ///     Keyed by the path in the container.
    /// </summary>
    public Dictionary<string, JsonElement>? Volumes { get; set; }

    public string? WorkingDir { get; set; }
    public string? User { get; set; }

    /// <summary>
    ///     Whether the image starts a bare shell, which exits at once when it runs in the background without a terminal and open input.
    /// </summary>
    [JsonIgnore]
    public bool StartsShell
    {
        get
        {
            string[] words = [.. Entrypoint ?? [], .. Cmd ?? []];
            return words is [var program, ..] && Shells.Contains(Path.GetFileName(program)) && !words.Contains("-c");
        }
    }
}

public sealed class VolumeDto
{
    public string Name { get; set; } = "";
    public string Driver { get; set; } = "";
    public string Mountpoint { get; set; } = "";
    public string Labels { get; set; } = "";

    [JsonIgnore]
    public bool IsAnonymous => Labels.Contains("com.docker.volume.anonymous", StringComparison.Ordinal);
}

public sealed class NetworkDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Driver { get; set; } = "";
    public string Internal { get; set; } = "";
    public string IPv6 { get; set; } = "";
    public string CreatedAt { get; set; } = "";

    public static bool IsBuiltInName(string name) => name is "bridge" or "host" or "none";
}

public sealed class WslcInfoDto
{
    public WslcClientInfoDto Client { get; set; } = new();
    public WslcServerInfoDto Server { get; set; } = new();
}

public sealed class WslcClientInfoDto
{
    public string Version { get; set; } = "";
}

public sealed class WslcServerInfoDto
{
    public string SessionManagerVersion { get; set; } = "";
    public List<WslcSessionDto> Sessions { get; set; } = [];
}

public sealed class WslcSessionDto
{
    public int Id { get; set; }
    public int CreatorPid { get; set; }
    public string Name { get; set; } = "";
}
