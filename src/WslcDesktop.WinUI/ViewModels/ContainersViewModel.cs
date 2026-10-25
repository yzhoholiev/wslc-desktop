using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

public sealed partial class ContainersViewModel : ListViewModel<ContainerItem>
{
    private static readonly Dictionary<string, SortColumn<ContainerItem>> Columns = new()
    {
        ["name"] = new SortColumn<ContainerItem>(item => item.Name),
        ["image"] = new SortColumn<ContainerItem>(item => item.Image),
        ["status"] = new SortColumn<ContainerItem>(item => item.StateRank),
        ["ports"] = new SortColumn<ContainerItem>(item => item.Ports),
        ["cpu"] = new SortColumn<ContainerItem>(item => ByteSize.ParsePercent(item.Cpu), true),
        ["memory"] = new SortColumn<ContainerItem>(item => ByteSize.Parse(item.Memory), true)
    };

    private readonly Dictionary<string, ContainerItem> _all = [];
    private CancellationTokenSource? _detailsLoad;
    private string? _error;
    private bool _hasSnapshot;
    private bool _isRuntimeStopped;
    private string? _pendingSelection;

    public ContainersViewModel(AppServices services)
        : base(services, "containers", "status", Columns)
    {
        Logs = new ContainerLogsViewModel(services.Details, services.Settings);
        ShowStopped = true;
    }

    [ObservableProperty]
    public partial bool ShowStopped { get; set; }

    [ObservableProperty]
    public partial bool HasRunning { get; set; }

    public ContainerLogsViewModel Logs { get; }

    public void Apply(RuntimeSnapshot snapshot)
    {
        UpdateBusy();
        _isRuntimeStopped = snapshot.Info is not null && !snapshot.IsRunning;
        _error = snapshot.Error;
        _hasSnapshot = snapshot.Info is not null || snapshot.Error is not null;

        var seen = new HashSet<string>();
        foreach (ContainerDto container in snapshot.Containers)
        {
            seen.Add(container.Id);
            if (!_all.TryGetValue(container.Id, out ContainerItem? item))
            {
                item = new ContainerItem(container);
                _all[container.Id] = item;
            }

            item.Update(container, snapshot.Stats.GetValueOrDefault(container.Id));
        }

        foreach (string id in _all.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _all.Remove(id);
        }

        Summary = $"{snapshot.RunningContainers} running · {snapshot.Containers.Count} total";
        HasRunning = snapshot.RunningContainers > 0;
        ApplyFilter();

        if (_pendingSelection is not null && _all.TryGetValue(_pendingSelection, out ContainerItem? pending))
        {
            _pendingSelection = null;
            SearchText = "";
            ShowStopped = true;
            Selected = pending;
            return;
        }

        if (Selected is not null && !_all.ContainsKey(Selected.Id))
        {
            Selected = null;
        }
        else
        {
            Logs.OnContainersUpdated();
        }
    }

    public void SelectWhenAvailable(string id) => _pendingSelection = id;

    public override Task RunEmptyActionAsync()
    {
        if (Empty.Reason != EmptyReason.NoneRunning)
        {
            return base.RunEmptyActionAsync();
        }

        ShowStopped = true;
        return Task.CompletedTask;
    }

    public Task StartOrStopAsync(ContainerItem item) => RunContainerOperationAsync(Services.Containers.StartOrStop(item.Id, item.Name, item.IsRunning));

    public Task RestartAsync(ContainerItem item) => RunContainerOperationAsync(Services.Containers.Restart(item.Id, item.Name));

    public Task RemoveAsync(ContainerItem item) => RunContainerOperationAsync(Services.Containers.Remove(item.Id, item.Name));

    public Task PruneAsync() =>
        RunContainerOperationAsync(new UserOperation("Removing stopped containers", Services.Commands.PruneContainersAsync, "Stopped containers removed."));

    [RelayCommand]
    private Task RefreshAsync() => WithBusyAsync(async () =>
    {
        await Services.Monitor.RefreshAsync();
        if (Selected is { } selected && _detailsLoad is { } load)
        {
            await LoadDetailsAsync(selected, load.Token);
        }
    });

    partial void OnShowStoppedChanged(bool value) => ApplyFilter();

    protected override string NameOf(ContainerItem item) => item.Name;

    protected override string KeyOf(ContainerItem item) => item.Id;

    protected override IEnumerable<ContainerItem> Filter(string search) =>
        _all.Values.Where(item => (ShowStopped || item.IsRunning) && item.Matches(search));

    protected override EmptyText DescribeEmpty(string search)
    {
        if (!_hasSnapshot)
        {
            return EmptyText.None;
        }

        if (_error is not null)
        {
            return EmptyText.Unavailable(_error, Services.Monitor.Latest.IsWslcMissing);
        }

        if (_isRuntimeStopped)
        {
            return EmptyText.RuntimeStopped("containers");
        }

        if (search.Length > 0)
        {
            return EmptyText.NoMatches(search);
        }

        return !ShowStopped && _all.Count > 0
            ? new EmptyText(EmptyReason.NoneRunning, Glyphs.Containers, "No running containers", $"{Humanize.Count(_all.Count, "stopped container")} hidden.",
                "Show stopped")
            : new EmptyText(EmptyReason.NoItems, Glyphs.Containers, "No containers yet", "Pull an image, then run a container from it on the Images page.",
                "Go to images");
    }

    protected override void OnSelectionChanged(ContainerItem? item)
    {
        CancellationTokenSource? previous = _detailsLoad;
        _detailsLoad = new CancellationTokenSource();

        // Switched first, so a read that the cancellation completes inline can no longer write to the pane.
        Logs.Show(item, _detailsLoad.Token);
        previous?.Cancel();
        InspectText = "";
        if (item is not null)
        {
            _ = LoadDetailsAsync(item, _detailsLoad.Token);
        }
    }

    private async Task LoadDetailsAsync(ContainerItem item, CancellationToken ct)
    {
        await Logs.LoadAsync(item);
        await PaneText.LoadAsync(() => Services.Details.InspectAsync(item.Id, ct), () => !ct.IsCancellationRequested, text => InspectText = text);
    }

    private Task RunContainerOperationAsync(UserOperation operation) =>
        RunAndReloadAsync(operation.Description, operation.Run, operation.SuccessMessage);
}
