using System.Collections.Immutable;
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
        ["size"] = new SortColumn<VolumeItem>(item => item.SizeBytes ?? -1, true),
        ["usage"] = new SortColumn<VolumeItem>(item => item.ContainerCount, true)
    };

    private IReadOnlyDictionary<string, long> _sizes = ImmutableDictionary<string, long>.Empty;

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
            ResourceUsage.ContainersUsingVolume(volume.Name, Services.Monitor.Latest.Containers).Length,
            SizeOf(volume.Name)))
    ];

    protected override void OnItemsLoaded(CancellationToken superseded) => _ = LoadSizesAsync(superseded);

    private async Task LoadSizesAsync(CancellationToken superseded)
    {
        IReadOnlyDictionary<string, long> sizes;
        try
        {
            sizes = await Services.Details.ReadVolumeSizesAsync(superseded);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (CliException)
        {
            // A size that could not be read again may no longer be true, as when a volume was recreated under its name.
            sizes = ImmutableDictionary<string, long>.Empty;
        }

        if (superseded.IsCancellationRequested)
        {
            return;
        }

        _sizes = sizes;
        UpdateItems(item => item with { SizeBytes = SizeOf(item.Name) }, superseded);
    }

    private long? SizeOf(string volume) => _sizes.TryGetValue(volume, out long bytes) ? bytes : null;

    protected override int CountIn(Inventory inventory) => inventory.Volumes;

    protected override ContainerDto[] ContainersUsing(VolumeItem item) => ResourceUsage.ContainersUsingVolume(item.Name, Services.Monitor.Latest.Containers);

    protected override string NameOf(VolumeItem item) => item.Name;

    protected override bool Matches(VolumeItem item, string search) =>
        item.Name.Contains(search, StringComparison.OrdinalIgnoreCase);

    protected override string Summarize(IReadOnlyList<VolumeItem> all)
    {
        string counts = $"{Humanize.Count(all.Count, "volume")} · {all.Count(item => item.IsAnonymous)} anonymous · {all.Count(item => item.IsUnused)} unused";
        return all.Any(item => item.SizeBytes is not null) ? $"{counts} · {ByteSize.Format(all.Sum(item => item.SizeBytes ?? 0))}" : counts;
    }

    protected override string KeyOf(VolumeItem item) => item.Name;

    protected override (string Title, string Subtitle) Describe(VolumeItem item)
    {
        string size = item.SizeBytes is null ? "" : $" · {item.Size}";
        return (item.DisplayName, $"{item.Kind} · {item.Driver}{size} · {Labels.UsageLabel(item.ContainerCount)} · {item.Mountpoint}");
    }

    protected override Task<string> InspectAsync(VolumeItem item, CancellationToken ct) =>
        Services.Details.InspectVolumeAsync(item.Name, ct);
}
