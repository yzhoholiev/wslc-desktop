using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

public sealed partial class NetworksViewModel(AppServices services) : ResourceListViewModel<NetworkItem>(services, "networks", "kind", Columns)
{
    private static readonly Dictionary<string, SortColumn<NetworkItem>> Columns = new()
    {
        ["name"] = new SortColumn<NetworkItem>(item => item.Name),
        ["driver"] = new SortColumn<NetworkItem>(item => item.Driver),
        ["kind"] = new SortColumn<NetworkItem>(item => item.Kind),
        ["created"] = new SortColumn<NetworkItem>(item => item.Created, true),
        ["usage"] = new SortColumn<NetworkItem>(item => item.ContainerCount, true)
    };

    protected override string ItemsName => "networks";

    protected override EmptyText NoItems { get; } =
        new(EmptyReason.NoItems, Glyphs.Networks, "No networks", "Networks appear here when you run wslc network create.");

    public Task RemoveAsync(NetworkItem item) =>
        RunAndReloadAsync($"Deleting network {item.Name}", () => Services.Commands.RemoveNetworkAsync(item.Name), $"Deleted network {item.Name}.");

    public Task PruneAsync() =>
        RunAndReloadAsync("Pruning networks", Services.Commands.PruneNetworksAsync, "Removed unused networks.");

    protected override async Task<List<NetworkItem>> FetchAsync() =>
    [
        .. (await Services.Reader.ListNetworksAsync(CancellationToken.None))
        .Select(network => new NetworkItem(
            network.Id,
            Humanize.ShortId(network.Id),
            network.Name,
            network.Driver,
            network.Internal == "true",
            network.CreatedAt.Length >= 19 ? network.CreatedAt[..19] : network.CreatedAt,
            ResourceUsage.ContainersUsingNetwork(network.Name, Services.Monitor.Latest.Containers).Length))
    ];

    protected override int CountIn(Inventory inventory) => inventory.Networks;

    protected override ContainerDto[] ContainersUsing(NetworkItem item) => ResourceUsage.ContainersUsingNetwork(item.Name, Services.Monitor.Latest.Containers);

    protected override string NameOf(NetworkItem item) => item.Name;

    protected override bool Matches(NetworkItem item, string search) =>
        item.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
        || item.Id.StartsWith(search, StringComparison.OrdinalIgnoreCase);

    protected override string Summarize(IReadOnlyList<NetworkItem> all) =>
        $"{Humanize.Count(all.Count, "network")} · {all.Count(item => !item.IsBuiltIn)} user-defined";

    protected override string KeyOf(NetworkItem item) => item.Id;

    protected override (string Title, string Subtitle) Describe(NetworkItem item) =>
        (item.Name, $"{item.ShortId} · {item.Driver} · {item.Kind}");

    protected override Task<string> InspectAsync(NetworkItem item, CancellationToken ct) =>
        Services.Details.InspectNetworkAsync(item.Name, ct);
}
