using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

/// <summary>
///     Searchable list of a wslc resource type that is loaded on demand rather than by the live monitor.
/// </summary>
public abstract partial class ResourceListViewModel<TItem> : ListViewModel<TItem>, IResourceListViewModel
    where TItem : class
{
    private readonly ReloadTrigger _reload;
    private List<TItem> _all = [];
    private bool _hasLoaded;
    private CancellationTokenSource? _inspectLoad;
    private bool _isRuntimeStopped;
    private int _loadGeneration;
    private Func<TItem, bool>? _pendingSelection;
    private string? _unavailableError;

    protected ResourceListViewModel(AppServices services, string page, string defaultSortKey, IReadOnlyDictionary<string, SortColumn<TItem>> sortColumns)
        : base(services, page, defaultSortKey, sortColumns)
    {
        DetailsTitle = "";
        DetailsSubtitle = "";
        _reload = new ReloadTrigger(CountIn);
    }

    /// <summary>
    ///     Plural name of the items, as used in sentences.
    /// </summary>
    protected abstract string ItemsName { get; }

    /// <summary>
    ///     Placeholder for when the runtime has none of these items at all.
    /// </summary>
    protected abstract EmptyText NoItems { get; }

    [ObservableProperty]
    public partial string? LoadError { get; set; }

    [ObservableProperty]
    public partial bool HasLoadError { get; set; }

    [ObservableProperty]
    public partial string DetailsTitle { get; set; }

    [ObservableProperty]
    public partial string DetailsSubtitle { get; set; }

    object? IResourceListViewModel.SelectedItem
    {
        get => Selected;
        set => Selected = (TItem?)value;
    }

    object IResourceListViewModel.Items => Items;

    ContainerDto[] IResourceListViewModel.ContainersUsing(object item) => ContainersUsing((TItem)item);

    public Task LoadAsync() => WithBusyAsync(LoadItemsAsync);

    /// <summary>
    ///     Reloads the list when the snapshot shows it may have changed, since the monitor does not poll it.
    /// </summary>
    public void OnRuntimeUpdated(RuntimeSnapshot snapshot)
    {
        UpdateBusy();
        if (_reload.ShouldReload(snapshot))
        {
            _ = LoadAsync();
        }
    }

    /// <summary>
    ///     Opens the details of the first item that matches, now or once the next load finishes.
    /// </summary>
    public void SelectWhenAvailable(Func<TItem, bool> predicate)
    {
        _pendingSelection = predicate;
        TrySelectPending();
    }

    [RelayCommand]
    private Task RefreshAsync() => WithBusyAsync(async () =>
    {
        // The list is skipped while the runtime looks stopped, so a stale "stopped" state must be re-checked first.
        await Services.Monitor.RefreshAsync();
        await LoadItemsAsync();
        if (Selected is { } selected)
        {
            await ReloadInspectAsync(selected);
        }
    });

    protected abstract Task<List<TItem>> FetchAsync();

    /// <summary>
    ///     How many of these items the monitor's inventory counts.
    /// </summary>
    protected abstract int CountIn(Inventory inventory);

    protected abstract ContainerDto[] ContainersUsing(TItem item);

    protected abstract bool Matches(TItem item, string search);

    protected abstract string Summarize(IReadOnlyList<TItem> all);

    protected abstract (string Title, string Subtitle) Describe(TItem item);

    protected abstract Task<string> InspectAsync(TItem item, CancellationToken ct);

    /// <summary>
    ///     Reloads the list after an operation changed it, then brings the monitor up to date.
    /// </summary>
    protected override async Task ReloadAsync()
    {
        await LoadAsync();
        _ = RefreshMonitorAsync();
    }

    /// <summary>
    ///     Refreshes the monitor without reloading the list again for the count change it brings.
    /// </summary>
    private async Task RefreshMonitorAsync()
    {
        _reload.BeginOwnRefresh();
        try
        {
            await Services.Monitor.RefreshAsync();
        }
        finally
        {
            _reload.EndOwnRefresh();
        }
    }

    protected override IEnumerable<TItem> Filter(string search) => _all.Where(item => search.Length == 0 || Matches(item, search));

    protected override EmptyText DescribeEmpty(string search) =>
        HasLoadError || !_hasLoaded ? EmptyText.None
        : _unavailableError is { } unavailable ? EmptyText.Unavailable(unavailable, Services.Monitor.Latest.IsWslcMissing)
        : _isRuntimeStopped ? EmptyText.RuntimeStopped(ItemsName)
        : search.Length > 0 ? EmptyText.NoMatches(search)
        : NoItems;

    protected override void OnSelectionRefreshed(TItem item) => (DetailsTitle, DetailsSubtitle) = Describe(item);

    protected override void OnSelectionChanged(TItem? item)
    {
        _inspectLoad?.Cancel();
        if (item is not null)
        {
            (DetailsTitle, DetailsSubtitle) = Describe(item);
            InspectText = "Loading…";
            _ = ReloadInspectAsync(item);
        }
    }

    protected override void ApplyFilter()
    {
        base.ApplyFilter();
        if (Selected is { } selected)
        {
            // A reload recreates the records, so the selection moves to the new record with the same key.
            Selected = _all.FirstOrDefault(item => KeyOf(item) == KeyOf(selected));
        }
    }

    private async Task LoadItemsAsync()
    {
        // Loads overlap (page load, refresh, a runtime change); only the newest may write the list.
        int generation = ++_loadGeneration;
        RuntimeSnapshot latest = Services.Monitor.Latest;

        _reload.Remember(latest);
        if (_reload.AwaitsFirstSample)
        {
            Summary = "Connecting to wslc…";
            return;
        }

        _unavailableError = latest is { Info: null, Error: { } error } ? error : null;
        _isRuntimeStopped = !latest.IsRunning;
        if (_isRuntimeStopped)
        {
            _all = [];
            Summary = _unavailableError ?? "The container runtime is stopped.";
            SetLoadError(null);
            _hasLoaded = true;
            ApplyFilter();
            return;
        }

        try
        {
            List<TItem> items = await FetchAsync();
            if (generation != _loadGeneration)
            {
                return;
            }

            _all = items;
            Summary = Summarize(_all);
            SetLoadError(null);
        }
        catch (CliException ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            SetLoadError(ex.Message);
        }

        _hasLoaded = true;
        ApplyFilter();
        TrySelectPending();

        // A request for an item that no longer exists must not fire on some later reload.
        _pendingSelection = null;
    }

    private void TrySelectPending()
    {
        if (_pendingSelection is null || _all.FirstOrDefault(_pendingSelection) is not { } match)
        {
            return;
        }

        _pendingSelection = null;
        SearchText = "";
        Selected = match;
    }

    private Task ReloadInspectAsync(TItem item)
    {
        _inspectLoad?.Cancel();
        _inspectLoad = new CancellationTokenSource();
        return LoadInspectAsync(item, _inspectLoad.Token);
    }

    private Task LoadInspectAsync(TItem item, CancellationToken ct) =>
        PaneText.LoadAsync(() => InspectAsync(item, ct), () => !ct.IsCancellationRequested, text => InspectText = text.TrimEnd());

    private void SetLoadError(string? error)
    {
        LoadError = error;
        HasLoadError = error is not null;
    }
}
