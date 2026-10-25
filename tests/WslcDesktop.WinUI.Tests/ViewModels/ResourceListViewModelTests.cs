using System.Collections.Specialized;
using WslcDesktop.Models;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.ViewModels;

public sealed partial class ResourceListViewModelTests
{
    private readonly TestServices _test = new();

    private readonly List<Thing> _things = [new("beta"), new("alpha"), new("gamma")];

    [Theory]
    [InlineData(null, EmptyReason.RuntimeStopped, "The container runtime is stopped.")]
    [InlineData("wslc did not respond in time.", EmptyReason.Unavailable, "wslc did not respond in time.")]
    public async Task LoadAsync_ExplainsAStoppedRuntimeOrUnreachableWslcWithoutFetching(string? error, EmptyReason expectedReason, string expectedSummary)
    {
        _test.Services.Monitor.Publish(error is null ? Stopped() : RuntimeSnapshot.Empty with { Error = error });
        var list = new ThingList(_test.Services, () => throw new InvalidOperationException("must not fetch"));

        await list.LoadAsync();

        Assert.True(list.IsEmpty);
        Assert.Equal(expectedReason, list.Empty.Reason);
        Assert.Equal(expectedSummary, list.Summary);
    }

    [Fact]
    public async Task LoadAsync_ListsSortedItemsWithASummary()
    {
        _test.Services.Monitor.Publish(Running());
        var list = new ThingList(_test.Services, () => Task.FromResult(_things));

        await list.LoadAsync();

        Assert.Equal(["alpha", "beta", "gamma"], list.Items.Select(thing => thing.Name));
        Assert.Equal("3 things", list.Summary);
        Assert.False(list.IsBusy);
    }

    [Fact]
    public async Task SearchText_FiltersAndExplainsNoMatches()
    {
        _test.Services.Monitor.Publish(Running());
        var list = new ThingList(_test.Services, () => Task.FromResult(_things));
        await list.LoadAsync();

        list.SearchText = "ga";
        Assert.Equal(["gamma"], list.Items.Select(thing => thing.Name));

        list.SearchText = "zzz";
        Assert.Equal(EmptyReason.NoMatches, list.Empty.Reason);
    }

    [Fact]
    public async Task RunEmptyActionAsync_StartsAStoppedRuntimeOrClearsTheSearch()
    {
        _test.Services.Monitor.Publish(Stopped());
        var list = new ThingList(_test.Services, () => Task.FromResult(_things));
        await list.LoadAsync();

        await list.RunEmptyActionAsync();
        Assert.Equal(["session-start"], _test.Commands.Calls);

        _test.Services.Monitor.Publish(Running());
        await list.LoadAsync();
        list.SearchText = "zzz";
        await list.RunEmptyActionAsync();

        Assert.Equal("", list.SearchText);
        Assert.Equal(3, list.Items.Count);
        Assert.Equal(["session-start"], _test.Commands.Calls);
    }

    [Fact]
    public async Task LoadAsync_ShowsTheNoItemsTextForAnEmptyRuntime()
    {
        _test.Services.Monitor.Publish(Running());
        var list = new ThingList(_test.Services, () => Task.FromResult(new List<Thing>()));

        await list.LoadAsync();

        Assert.Equal(EmptyReason.NoItems, list.Empty.Reason);
        Assert.Equal("No things", list.Empty.Title);
    }

    [Fact]
    public async Task LoadAsync_ReportsAFailedFetchInsteadOfAnEmptyState()
    {
        _test.Services.Monitor.Publish(Running());
        var list = new ThingList(_test.Services, () => throw new CliException("wslc broke"));

        await list.LoadAsync();

        Assert.True(list.HasLoadError);
        Assert.Equal("wslc broke", list.LoadError);
        Assert.Equal(EmptyReason.None, list.Empty.Reason);
    }

    [Fact]
    public async Task Selecting_DescribesAndInspectsTheItem()
    {
        _test.Services.Monitor.Publish(Running());
        var list = new ThingList(_test.Services, () => Task.FromResult(_things));
        await list.LoadAsync();

        // The fake inspection completes synchronously, so the pane is filled once Selected is set.
        list.Selected = list.Items[0];

        Assert.True(list.IsDetailsOpen);
        Assert.Equal("alpha", list.DetailsTitle);
        Assert.Equal("a thing", list.DetailsSubtitle);
        Assert.Equal("inspect alpha", list.InspectText);

        list.CloseDetails();
        Assert.False(list.IsDetailsOpen);
    }

    [Fact]
    public async Task SelectWhenAvailable_OpensTheItemAfterTheNextLoadAndClearsTheSearch()
    {
        _test.Services.Monitor.Publish(Running());
        var list = new ThingList(_test.Services, () => Task.FromResult(_things)) { SearchText = "beta" };

        list.SelectWhenAvailable(thing => thing.Name == "gamma");
        Assert.Null(list.Selected);

        await list.LoadAsync();

        Assert.Equal("gamma", list.Selected?.Name);
        Assert.Equal("", list.SearchText);
    }

    [Fact]
    public async Task SelectWhenAvailable_ForgetsAnItemThatDoesNotExist()
    {
        _test.Services.Monitor.Publish(Running());
        var list = new ThingList(_test.Services, () => Task.FromResult(_things));
        list.SelectWhenAvailable(thing => thing.Name == "missing");
        await list.LoadAsync();

        _things.Add(new Thing("missing"));
        await list.LoadAsync();

        Assert.Null(list.Selected);
    }

    [Fact]
    public async Task SelectedItemsDeletedByAReload_AreDeselected()
    {
        _test.Services.Monitor.Publish(Running());
        List<Thing> current = _things;
        var list = new ThingList(_test.Services, () => Task.FromResult(current));
        await list.LoadAsync();
        list.Selected = list.Items.First(thing => thing.Name == "beta");

        current = [new Thing("alpha")];
        await list.LoadAsync();

        Assert.Null(list.Selected);
    }

    [Fact]
    public async Task LoadAsync_WaitsForTheFirstSampleInsteadOfReportingAStoppedRuntime()
    {
        _test.Services.Monitor.Publish(RuntimeSnapshot.Empty);
        var list = new ThingList(_test.Services, () => Task.FromResult(_things));

        await list.LoadAsync();
        Assert.Equal(EmptyReason.None, list.Empty.Reason);
        Assert.Empty(list.Items);

        _test.Services.Monitor.Publish(Running());
        list.OnRuntimeUpdated(Running());

        Assert.Equal(3, list.Items.Count);
    }

    [Fact]
    public void Empty_StaysBlankUntilTheFirstLoad()
    {
        _test.Services.Monitor.Publish(Running());

        Assert.Equal(EmptyReason.None, new ThingList(_test.Services, () => Task.FromResult(_things)).Empty.Reason);
    }

    [Fact]
    public async Task OnRuntimeUpdated_ReloadsWhenTheRuntimeStartsEvenWhileBusy()
    {
        _test.Services.Monitor.Publish(Stopped());
        int fetches = 0;
        var list = new ThingList(_test.Services, () =>
        {
            fetches++;
            return Task.FromResult(_things);
        });

        await list.LoadAsync();
        list.OnRuntimeUpdated(Stopped());

        // The page's Start button keeps it busy while the monitor publishes the started runtime.
        _test.Services.Monitor.Publish(Running());
        await list.RunWhileBusyAsync(() => list.OnRuntimeUpdated(Running()));

        Assert.Equal(1, fetches);
        Assert.Equal(3, list.Items.Count);
    }

    [Fact]
    public async Task OnRuntimeUpdated_ReloadsWhenTheInventoryCountChanges()
    {
        _test.Services.Monitor.Publish(Running());
        int fetches = 0;
        var list = new ThingList(_test.Services, () =>
        {
            fetches++;
            return Task.FromResult(_things);
        });

        await list.LoadAsync();

        list.OnRuntimeUpdated(WithThings(3));
        list.OnRuntimeUpdated(WithThings(3));
        Assert.Equal(1, fetches);

        list.OnRuntimeUpdated(WithThings(4));
        Assert.Equal(2, fetches);
    }

    [Fact]
    public async Task OnRuntimeUpdated_ReloadsWhenWslcBecomesReachable()
    {
        RuntimeSnapshot unreachable = RuntimeSnapshot.Empty with { Error = "wslc did not respond in time." };
        _test.Services.Monitor.Publish(unreachable);
        var list = new ThingList(_test.Services, () => Task.FromResult(_things));
        await list.LoadAsync();
        list.OnRuntimeUpdated(unreachable);

        _test.Services.Monitor.Publish(Stopped());
        list.OnRuntimeUpdated(Stopped());

        Assert.Equal(EmptyReason.RuntimeStopped, list.Empty.Reason);
    }

    [Fact]
    public async Task LoadAsync_IgnoresAnOlderLoadThatFinishesLast()
    {
        _test.Services.Monitor.Publish(Running());
        var slow = new TaskCompletionSource<List<Thing>>();
        var fetches = new Queue<Task<List<Thing>>>([slow.Task, Task.FromResult(new List<Thing> { new("new") })]);
        var list = new ThingList(_test.Services, () => fetches.Dequeue());

        Task older = list.LoadAsync();
        await list.LoadAsync();
        slow.SetResult([new Thing("old")]);
        await older;

        Assert.Equal(["new"], list.Items.Select(thing => thing.Name));
    }

    [Fact]
    public void InterfaceMembers_ForwardToTheTypedList()
    {
        IResourceListViewModel list = new ThingList(_test.Services, () => Task.FromResult(_things));

        list.SelectedItem = _things[1];

        Assert.Same(_things[1], ((ThingList)list).Selected);
        Assert.Equal(["used-by"], list.ContainersUsing(_things[1]).Select(container => container.Names));
    }

    [Fact]
    public async Task Reload_ReplacesAChangedRowInPlaceAndKeepsItSelected()
    {
        _test.Services.Monitor.Publish(Running());
        var list = new ThingList(_test.Services, () => Task.FromResult(_things));
        await list.LoadAsync();
        list.Selected = list.Items.First(thing => thing.Name == "beta");
        List<NotifyCollectionChangedAction> actions = [];
        list.Items.CollectionChanged += (_, e) => actions.Add(e.Action);

        _things[0] = new Thing("beta", "2 MB");
        await list.LoadAsync();

        Assert.Equal([NotifyCollectionChangedAction.Replace], actions);
        Assert.Same(_things[0], list.Selected);
        Assert.Equal("a thing 2 MB", list.DetailsSubtitle);
        Assert.Equal(1, list.Inspections);
    }

    private static RuntimeSnapshot WithThings(int count) => Running([], new Dictionary<string, ContainerStatsDto>(), new Inventory(count, 0, 0, 0, 0, 0, 0, 0));

    private sealed record Thing(string Name, string Size = "");

    private sealed partial class ThingList(AppServices services, Func<Task<List<Thing>>> fetch)
        : ResourceListViewModel<Thing>(services, $"things-{Guid.NewGuid():N}", "name", Columns)
    {
        private static readonly Dictionary<string, SortColumn<Thing>> Columns = new() { ["name"] = new SortColumn<Thing>(thing => thing.Name) };

        protected override string ItemsName => "things";

        protected override EmptyText NoItems { get; } = new(EmptyReason.NoItems, "", "No things");

        public int Inspections { get; private set; }

        protected override string NameOf(Thing item) => item.Name;

        protected override Task<List<Thing>> FetchAsync() => fetch();

        protected override int CountIn(Inventory inventory) => inventory.Images;

        protected override ContainerDto[] ContainersUsing(Thing item) => [Container("used-by")];

        protected override bool Matches(Thing item, string search) => item.Name.Contains(search, StringComparison.OrdinalIgnoreCase);

        protected override string Summarize(IReadOnlyList<Thing> all) => $"{all.Count} things";

        protected override string KeyOf(Thing item) => item.Name;

        protected override (string Title, string Subtitle) Describe(Thing item) => (item.Name, $"a thing {item.Size}".TrimEnd());

        protected override Task<string> InspectAsync(Thing item, CancellationToken ct)
        {
            Inspections++;
            return Task.FromResult($"inspect {item.Name}");
        }

        public Task RunWhileBusyAsync(Action action) => WithBusyAsync(() =>
        {
            action();
            return Task.CompletedTask;
        });
    }
}
