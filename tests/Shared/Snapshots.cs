using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests;

/// <summary>
///     Builders for runtime snapshots and containers as the monitor would publish them.
/// </summary>
internal static class Snapshots
{
    public static WslcInfoDto Info(params string[] sessions) => new()
    {
        Client = new WslcClientInfoDto { Version = "3.0.3.0" },
        Server = new WslcServerInfoDto
        {
            SessionManagerVersion = "3.0.3",
            Sessions = [.. sessions.Select((name, index) => new WslcSessionDto { Id = index + 1, CreatorPid = 100 + index, Name = name })]
        }
    };

    public static RuntimeSnapshot Running(params ContainerDto[] containers) => Running(containers, new Dictionary<string, ContainerStatsDto>());

    public static RuntimeSnapshot Running(IReadOnlyList<ContainerDto> containers, IReadOnlyDictionary<string, ContainerStatsDto> stats,
        Inventory? inventory = null) =>
        new(Info(Wslc.DefaultSessionName), containers, stats, null, null, inventory, null);

    public static RuntimeSnapshot Stopped(params string[] otherSessions) => RuntimeSnapshot.Empty with { Info = Info(otherSessions) };

    public static ContainerDto Container(
        string name,
        string state = "running",
        string image = "busybox",
        string ports = "",
        string mounts = "",
        string networks = "",
        string imageId = "")
    {
        return new ContainerDto
        {
            Id = $"{name}-id-0123456789abcdef",
            Names = name,
            Image = image,
            State = state,
            Status = state == "running" ? "Up 5 minutes" : "Exited (0) 1 minute ago",
            Ports = ports,
            Mounts = mounts,
            Networks = networks,
            ImageId = imageId
        };
    }

    public static ContainerStatsDto Stats(ContainerDto container, string cpu, string memory) => new()
    {
        Id = container.Id,
        CpuPerc = cpu,
        MemUsage = $"{memory} / 1GiB"
    };
}
