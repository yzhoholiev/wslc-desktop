namespace WslcDesktop.Models;

public sealed record PortMapping(string HostPort, string ContainerPort);

/// <summary>
///     A host folder or named volume mounted at a path in the container.
/// </summary>
public sealed record VolumeMapping(string Source, string Target);

/// <summary>
///     What a container is created with; blank values leave the image's defaults.
/// </summary>
public sealed record ContainerRunOptions(string Image)
{
    public string Name { get; init; } = "";

    public PortMapping[] Ports { get; init; } = [];

    public bool PublishAllPorts { get; init; }

    public VolumeMapping[] Volumes { get; init; } = [];

    public KeyValueEntry[] Environment { get; init; } = [];

    public string EnvironmentFile { get; init; } = "";

    /// <summary>
    ///     The command and its arguments, quoted as in a POSIX shell.
    /// </summary>
    public string Command { get; init; } = "";

    public string Entrypoint { get; init; } = "";

    public string WorkingDirectory { get; init; } = "";

    public string User { get; init; } = "";

    public string Network { get; init; } = "";

    public string Hostname { get; init; } = "";

    public string Memory { get; init; } = "";

    public string Cpus { get; init; } = "";

    public bool Tty { get; init; }

    public bool Interactive { get; init; }

    public bool RemoveWhenStopped { get; init; }

    public bool AllGpus { get; init; }
}

/// <summary>
///     An image's own defaults and the networks a new container can join.
/// </summary>
public sealed record RunImageDefaults(string Image, string Reference, ImageConfigDto Config, string[] Networks);
