using CommunityToolkit.Mvvm.ComponentModel;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

public sealed partial class ContainerItem(ContainerDto container) : ObservableObject
{
    public string Id { get; } = container.Id;

    public string ShortId { get; } = Humanize.ShortId(container.Id);

    public string Name { get; } = container.Names;

    public string Image { get; } = container.Image;

    [ObservableProperty]
    public partial string ImageId { get; set; } = container.ImageId;

    [ObservableProperty]
    public partial string Mounts { get; set; } = container.Mounts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    public partial string State { get; set; } = "";

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    [ObservableProperty]
    public partial string Ports { get; set; } = "";

    [ObservableProperty]
    public partial string Cpu { get; set; } = "";

    [ObservableProperty]
    public partial string Memory { get; set; } = "";

    public bool IsRunning => State == "running";

    /// <summary>
    ///     Orders states from active to inactive.
    /// </summary>
    public int StateRank => State switch
    {
        "running" => 0,
        "paused" or "restarting" => 1,
        _ => 2
    };

    public void Update(ContainerDto container, ContainerStatsDto? stats)
    {
        ImageId = container.ImageId;
        Mounts = container.Mounts;
        State = container.State;
        Status = container.Status;
        Ports = container.Ports;
        Cpu = stats?.CpuPerc ?? "—";
        Memory = stats is null ? "—" : ByteSize.Normalize(stats.MemUsed);
    }

    public bool Matches(string search) =>
        search.Length == 0
        || Name.Contains(search, StringComparison.OrdinalIgnoreCase)
        || Image.Contains(search, StringComparison.OrdinalIgnoreCase)
        || Id.StartsWith(search, StringComparison.OrdinalIgnoreCase);
}
