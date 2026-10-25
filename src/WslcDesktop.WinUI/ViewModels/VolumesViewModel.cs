using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

public sealed partial class VolumesViewModel(AppServices services) : ResourceListViewModel<VolumeItem>(services, "volumes", "name", Columns)
{
    private static readonly Dictionary<string, SortColumn<VolumeItem>> Columns = new()
    {
        // Named volumes come first, since anonymous ones are hashes that would otherwise bury them.
        ["name"] = new SortColumn<VolumeItem>(item => (item.IsAnonymous, item.Name.ToLowerInvariant())),
        ["driver"] = new SortColumn<VolumeItem>(item => item.Driver),
        ["mountpoint"] = new SortColumn<VolumeItem>(item => item.Mountpoint),
        ["usage"] = new SortColumn<VolumeItem>(item => item.ContainerCount, true)
    };

    protected override string ItemsName => "volumes";

    protected override EmptyText NoItems { get; } =
        new(EmptyReason.NoItems, Glyphs.Volumes, "No volumes", "Volumes appear here when containers create them or you run wslc volume create.");

    public Task RemoveAsync(VolumeItem item) =>
        RunAndReloadAsync($"Deleting volume {item.DisplayName}", () => Services.Commands.RemoveVolumeAsync(item.Name), $"Deleted volume {item.DisplayName}.");

    public Task PruneAsync(bool all) =>
        RunAndReloadAsync("Pruning volumes", () => Services.Commands.PruneVolumesAsync(all),
            all ? "Removed all unused volumes." : "Removed unused anonymous volumes.");

    protected override async Task<List<VolumeItem>> FetchAsync() =>
    [
        .. (await Services.Reader.ListVolumesAsync(CancellationToken.None))
        .Select(volume => new VolumeItem(
            volume.Name,
            volume.Name.Length == 64 ? volume.Name[..12] : volume.Name,
            volume.Driver,
            volume.Mountpoint,
            volume.IsAnonymous,
            ResourceUsage.ContainersUsingVolume(volume.Name, Services.Monitor.Latest.Containers).Length))
    ];

    protected override int CountIn(Inventory inventory) => inventory.Volumes;

    protected override ContainerDto[] ContainersUsing(VolumeItem item) => ResourceUsage.ContainersUsingVolume(item.Name, Services.Monitor.Latest.Containers);

    protected override string NameOf(VolumeItem item) => item.Name;

    protected override bool Matches(VolumeItem item, string search) =>
        item.Name.Contains(search, StringComparison.OrdinalIgnoreCase);

    protected override string Summarize(IReadOnlyList<VolumeItem> all) =>
        $"{Humanize.Count(all.Count, "volume")} · {all.Count(item => item.IsAnonymous)} anonymous · {all.Count(item => item.IsUnused)} unused";

    protected override string KeyOf(VolumeItem item) => item.Name;

    protected override (string Title, string Subtitle) Describe(VolumeItem item) =>
        (item.DisplayName, $"{item.Kind} · {item.Driver} · {Labels.UsageLabel(item.ContainerCount)} · {item.Mountpoint}");

    protected override Task<string> InspectAsync(VolumeItem item, CancellationToken ct) =>
        Services.Details.InspectVolumeAsync(item.Name, ct);
}
