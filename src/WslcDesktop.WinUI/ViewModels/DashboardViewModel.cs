using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

public sealed partial class DashboardViewModel : PageViewModel
{
    private const int MinimumGraphSamples = 3;

    /// <summary>
    ///     The banner's list shows this many containers; the Containers page has the rest.
    /// </summary>
    private const int MaxAttentionContainers = 4;

    private const int MaxEndpoints = 5;
    private const int TopCount = 3;
    private const string GraphWindow = "last 3 minutes";

    private AttentionItem[] _attention = [];

    public DashboardViewModel(AppServices services)
        : base(services)
    {
        StatusText = "Connecting to wslc…";
        IsAttentionOpen = services.Settings.Current.IsAttentionOpen;
    }

    /// <summary>
    ///     The rows of the opened attention banner.
    /// </summary>
    public ObservableCollection<AttentionItem> AttentionItems { get; } = [];

    public ObservableCollection<UsageBar> TopCpu { get; } = [];

    public ObservableCollection<UsageBar> TopMemory { get; } = [];

    public ObservableCollection<EndpointRow> Endpoints { get; } = [];

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial bool IsStopped { get; set; }

    /// <summary>
    ///     Whether nothing is known about the runtime yet.
    /// </summary>
    [ObservableProperty]
    public partial bool IsConnecting { get; set; } = true;

    /// <summary>
    ///     Whether the runtime runs without any image or container, which shows how to get the first one in place of empty cards.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFirstRun { get; set; }

    /// <summary>
    ///     Whether the banner, counts and resource cards show.
    /// </summary>
    [ObservableProperty]
    public partial bool HasOverview { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial string? ErrorText { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial bool IsWslcMissing { get; set; }

    [ObservableProperty]
    public partial string ErrorTitle { get; set; } = "";

    /// <summary>
    ///     Whether anything needs attention; the banner shows only then, so that its appearing is the signal.
    /// </summary>
    [ObservableProperty]
    public partial bool HasAttention { get; set; }

    /// <summary>
    ///     The tone of the worst attention item, which names the banner's styles.
    /// </summary>
    [ObservableProperty]
    public partial string AttentionTone { get; set; } = "Caution";

    [ObservableProperty]
    public partial string AttentionTitle { get; set; } = "";

    [ObservableProperty]
    public partial string AttentionSummary { get; set; } = "";

    /// <summary>
    ///     The only attention item, which the banner then shows itself in place of a summary.
    /// </summary>
    [ObservableProperty]
    public partial AttentionItem? SingleAttention { get; set; }

    /// <summary>
    ///     Whether there are several attention items, which the banner summarizes and lists on request.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAttentionListVisible))]
    public partial bool HasAttentionList { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AttentionToggleText), nameof(IsAttentionListVisible))]
    public partial bool IsAttentionOpen { get; set; }

    public bool IsAttentionListVisible => HasAttentionList && IsAttentionOpen;

    /// <summary>
    ///     Says how many failed containers the list leaves to the Containers page, or nothing.
    /// </summary>
    [ObservableProperty]
    public partial string MoreAttentionText { get; set; } = "";

    public string AttentionToggleText => IsAttentionOpen ? "Hide" : "Show";

    [ObservableProperty]
    public partial string ContainersCount { get; set; } = "0";

    [ObservableProperty]
    public partial string ContainersText { get; set; } = "";

    [ObservableProperty]
    public partial string ImagesCount { get; set; } = "0";

    [ObservableProperty]
    public partial string ImagesText { get; set; } = "";

    [ObservableProperty]
    public partial string VolumesCount { get; set; } = "0";

    [ObservableProperty]
    public partial string VolumesText { get; set; } = "";

    [ObservableProperty]
    public partial string NetworksCount { get; set; } = "0";

    [ObservableProperty]
    public partial string NetworksText { get; set; } = "";

    /// <summary>
    ///     The VM's size and load, and the time the graph covers.
    /// </summary>
    [ObservableProperty]
    public partial string VmSummary { get; set; } = GraphWindow;

    [ObservableProperty]
    public partial string CpuValue { get; set; } = "–";

    [ObservableProperty]
    public partial string CpuCaption { get; set; } = "";

    [ObservableProperty]
    public partial double[] CpuHistory { get; set; } = [];

    [ObservableProperty]
    public partial string MemoryValue { get; set; } = "–";

    [ObservableProperty]
    public partial string MemoryCaption { get; set; } = "";

    [ObservableProperty]
    public partial double[] MemoryHistory { get; set; } = [];

    /// <summary>
    ///     The VM's memory in bytes, which turns the graph's memory scale from percent into a size.
    /// </summary>
    [ObservableProperty]
    public partial double MemoryTotal { get; set; }

    [ObservableProperty]
    public partial string StorageValue { get; set; } = "–";

    [ObservableProperty]
    public partial string StorageCaption { get; set; } = "";

    [ObservableProperty]
    public partial string StorageImagesText { get; set; } = "";

    [ObservableProperty]
    public partial string StorageOtherText { get; set; } = "";

    /// <summary>
    ///     How much of the used container storage the images take, from 0 to 1.
    /// </summary>
    [ObservableProperty]
    public partial double StorageImagesShare { get; set; }

    [ObservableProperty]
    public partial bool HasStorage { get; set; }

    /// <summary>
    ///     Whether the graph has enough samples to draw a line rather than a dot.
    /// </summary>
    [ObservableProperty]
    public partial bool HasHistory { get; set; }

    [ObservableProperty]
    public partial bool HasRunningContainers { get; set; }

    [ObservableProperty]
    public partial bool HasTopContainers { get; set; }

    [ObservableProperty]
    public partial bool HasEndpoints { get; set; }

    /// <summary>
    ///     Says how many publishing containers the card leaves to the Containers page, or nothing.
    /// </summary>
    [ObservableProperty]
    public partial string MoreEndpointsText { get; set; } = "";

    private Dictionary<string, long> Dismissed => Services.Settings.Current.DismissedAttention;

    public void Apply(RuntimeSnapshot snapshot)
    {
        RuntimeMonitor monitor = Services.Monitor;

        UpdateBusy();
        IsRunning = snapshot.IsRunning;
        IsStopped = snapshot.Info is not null && !snapshot.IsRunning;
        IsConnecting = snapshot.Info is null && snapshot.Error is null;
        ErrorText = snapshot.Error;
        HasError = snapshot.Error is not null;
        IsWslcMissing = snapshot.IsWslcMissing;
        ErrorTitle = snapshot.IsWslcMissing ? "wslc is not installed" : "Could not query wslc";
        List<string> otherSessions = snapshot.Info?.Server.Sessions
            .Select(session => session.Name)
            .Where(name => name != Wslc.DefaultSessionName)
            .ToList() ?? [];

        StatusText = (snapshot.IsRunning, otherSessions.Count) switch
        {
            (false, _) when monitor.Operation == RuntimeActions.StartRuntimeDescription => $"{RuntimeActions.StartRuntimeDescription}…",
            (true, _) when snapshot.Vm is { } vm => $"Up {FormatUptime(vm.Uptime)} · session {Wslc.DefaultSessionName}",
            (true, _) => $"Session {Wslc.DefaultSessionName}",
            (false, 0) => "The container runtime is stopped. It starts on demand when you run a container command.",
            (false, _) => $"Session {Wslc.DefaultSessionName} is stopped. Also running, with separate containers: {string.Join(", ", otherSessions)}."
        };

        IsFirstRun = snapshot is { IsRunning: true, Containers.Count: 0, Inventory.Images: 0 };
        HasOverview = snapshot.IsRunning && !IsFirstRun;

        ApplyInventory(snapshot);
        ApplyVm(snapshot.Vm);
        CpuHistory = monitor.CpuHistory;
        MemoryHistory = monitor.MemoryHistory;
        HasHistory = CpuHistory.Length >= MinimumGraphSamples;
        ApplyStorage(snapshot);
        ApplyTopContainers(snapshot);
        ApplyEndpoints(snapshot);

        if (snapshot.IsRunning && DashboardAttention.Forget(snapshot, Dismissed))
        {
            Services.Settings.Save();
        }

        ApplyAttention(snapshot);
    }

    [RelayCommand]
    private Task RefreshAsync() => WithBusyAsync(Services.Monitor.RefreshAsync);

    [RelayCommand]
    private Task OpenWslcSettingsAsync() => RunOperationAsync("Opening wslc settings", Services.Commands.OpenSettingsAsync);

    [RelayCommand]
    private Task UpdateWslAsync() => RunOperationAsync(
        "Updating WSL",
        async () =>
        {
            await Services.Actions.UpdateWslAsync(false);
            await Services.Monitor.RefreshAsync();
        });

    [RelayCommand]
    private void OpenWslcDocumentation() => Services.Shell.OpenUrl(Wslc.DocumentationUrl);

    [RelayCommand]
    private void ToggleAttention()
    {
        IsAttentionOpen = !IsAttentionOpen;
        Services.Settings.Current.IsAttentionOpen = IsAttentionOpen;
        Services.Settings.Save();
    }

    /// <summary>
    ///     Hides an attention item until it happens again or grows.
    /// </summary>
    public void Dismiss(AttentionItem item) => Dismiss(new[] { item });

    [RelayCommand]
    private void DismissAll() => Dismiss(_attention);

    /// <summary>
    ///     Starts a failed container again, or stops one that keeps restarting.
    /// </summary>
    public Task StartOrStopAsync(AttentionItem item)
    {
        UserOperation operation = Services.Containers.StartOrStop(item.ContainerId, item.Subject, item.Kind == AttentionKind.ContainerRestarting);
        return RunAndReloadAsync(operation.Description, operation.Run, operation.SuccessMessage);
    }

    public Task ShutdownWslAsync() => RunOperationAsync("Shutting down WSL", Services.Actions.ShutdownWslAsync, "WSL has been shut down.");

    public CleanupSummary GetCleanupSummary()
    {
        RuntimeSnapshot snapshot = Services.Monitor.Latest;
        return new CleanupSummary(
            snapshot.RunningContainers,
            snapshot.Containers.Count(container => !container.IsActive),
            snapshot.Inventory?.Images ?? 0,
            ByteSize.Format(snapshot.Inventory?.ImagesSize ?? 0),
            snapshot.Inventory?.Volumes ?? 0);
    }

    public Task StopRuntimeAsync() =>
        RunRuntimeOperationAsync("Stopping the container runtime", Services.Actions.StopRuntimeAsync, "The container runtime has been stopped.");

    public Task CleanupAsync(CleanupOptions options) => RunRuntimeOperationAsync(
        "Cleaning up",
        async () =>
        {
            CleanupResult result = await Services.Cleanup.RunAsync(options);
            await Services.Monitor.RefreshAsync();

            if (result.Failures.Count > 0)
            {
                Services.Notifier.Error("Cleanup partly failed", string.Join(Environment.NewLine, result.Failures));
            }
            else
            {
                Services.Notifier.Success($"Cleanup finished — reclaimed {ByteSize.Format(result.ReclaimedBytes)}.");
            }
        });

    private void Dismiss(AttentionItem[] items)
    {
        foreach (AttentionItem item in items.Where(item => item.CanDismiss))
        {
            Dismissed[item.Key] = item.Value;
        }

        Services.Settings.Save();
        ApplyAttention(Services.Monitor.Latest);
    }

    private void ApplyAttention(RuntimeSnapshot snapshot)
    {
        _attention = snapshot.IsRunning ? DashboardAttention.Build(snapshot, Dismissed) : [];
        SingleAttention = _attention.Length == 1 ? _attention[0] : null;
        HasAttentionList = _attention.Length > 1;

        List<AttentionItem> containers = [.. _attention.Where(item => item.Kind != AttentionKind.UnusedImages)];
        AttentionItem[] shown = [.. containers.Take(MaxAttentionContainers), .. _attention.Where(item => item.Kind == AttentionKind.UnusedImages)];
        AttentionItems.ReplaceWith(shown);
        int more = containers.Count - MaxAttentionContainers;
        MoreAttentionText = more > 0 ? $"{Humanize.Count(more, "more container")} · show them on the Containers page" : "";

        HasAttention = _attention.Length > 0;
        AttentionTone = _attention.Any(item => item.Tone == "Critical") ? "Critical" : "Caution";
        AttentionTitle = $"{_attention.Length} things need attention";
        AttentionSummary = DashboardAttention.Summarize(_attention);
    }

    private void ApplyVm(VmMetrics? vm)
    {
        if (vm is null)
        {
            CpuValue = MemoryValue = "–";
            CpuCaption = MemoryCaption = "";
            VmSummary = GraphWindow;
            return;
        }

        double[] cpu = Services.Monitor.CpuHistory;
        CpuValue = $"{vm.CpuPercent:0.0}%";
        CpuCaption = cpu.Length > 0 ? $"peak {cpu.Max():0.0}%" : "";
        MemoryValue = ByteSize.Format(vm.MemoryUsed);
        MemoryTotal = vm.MemoryTotal;
        string swap = vm.SwapTotal > 0 ? $"swap {ByteSize.Format(vm.SwapUsed)}" : "no swap";
        MemoryCaption = $"of {ByteSize.Format(vm.MemoryTotal)} · {swap}";
        string load = vm.LoadAverage.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "–";
        VmSummary = $"{vm.VirtualCpus} vCPUs · {ByteSize.Format(vm.MemoryTotal)} · load {load} · {GraphWindow}";
    }

    private void ApplyStorage(RuntimeSnapshot snapshot)
    {
        HasStorage = snapshot.Storage is not null;
        if (snapshot.Storage is not { } storage)
        {
            StorageValue = "–";
            StorageCaption = StorageImagesText = StorageOtherText = "";
            StorageImagesShare = 0;
            return;
        }

        long images = Math.Min(snapshot.Inventory?.ImagesSize ?? 0, storage.Used);
        long unused = snapshot.Inventory?.UnusedImagesSize ?? 0;
        StorageValue = ByteSize.Format(storage.Used);
        StorageCaption = unused > 0 ? $"{ByteSize.Format(unused)} reclaimable" : "Nothing to reclaim";
        StorageImagesText = ByteSize.Format(images);
        StorageOtherText = ByteSize.Format(storage.Used - images);
        StorageImagesShare = storage.Used == 0 ? 0 : (double)images / storage.Used;
    }

    private static string FormatUptime(TimeSpan uptime) => uptime switch
    {
        { TotalDays: >= 1 } => $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m",
        { TotalHours: >= 1 } => $"{uptime.Hours}h {uptime.Minutes}m",
        { TotalMinutes: >= 1 } => $"{uptime.Minutes}m",
        _ => $"{uptime.Seconds}s"
    };

    private void ApplyInventory(RuntimeSnapshot snapshot)
    {
        int running = snapshot.RunningContainers;
        int containers = snapshot.Containers.Count;
        HasRunningContainers = snapshot.IsRunning && running > 0;
        (ContainersCount, ContainersText) = containers == 0 ? ("0", "") : ($"{running}", $"of {containers} running");

        if (snapshot.Inventory is not { } inventory)
        {
            return;
        }

        ImagesCount = $"{inventory.Images}";
        ImagesText = inventory.Images == 0 ? "" : ByteSize.Format(inventory.ImagesSize);
        VolumesCount = $"{inventory.Volumes}";
        VolumesText = inventory.AnonymousVolumes > 0 ? $"{inventory.AnonymousVolumes} anonymous" : "";
        NetworksCount = $"{inventory.Networks}";
        NetworksText = inventory.CustomNetworks > 0 ? $"{inventory.CustomNetworks} user-defined" : "built-in";
    }

    private void ApplyTopContainers(RuntimeSnapshot snapshot)
    {
        var usage = snapshot.Containers
            .Select(container => (Container: container, Stats: snapshot.Stats.GetValueOrDefault(container.Id)))
            .Where(pair => pair.Stats is not null)
            .Select(pair => (
                Name: pair.Container.Names,
                CpuText: pair.Stats!.CpuPerc,
                Cpu: ByteSize.ParsePercent(pair.Stats.CpuPerc),
                MemoryText: ByteSize.Normalize(pair.Stats.MemUsed),
                Memory: ByteSize.Parse(pair.Stats.MemUsed)))
            .ToList();

        // Scaling CPU against at least one full core keeps a nearly idle top container from showing a full bar.
        double cpuScale = Math.Max(100, usage.Select(item => item.Cpu).DefaultIfEmpty().Max());
        long memoryScale = Math.Max(1, usage.Select(item => item.Memory).DefaultIfEmpty().Max());

        TopCpu.ReplaceWith(usage
            .OrderByDescending(item => item.Cpu)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(TopCount)
            .Select(item => new UsageBar(item.Name, item.CpuText, item.Cpu / cpuScale))
            .ToArray());

        TopMemory.ReplaceWith(usage
            .OrderByDescending(item => item.Memory)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(TopCount)
            .Select(item => new UsageBar(item.Name, item.MemoryText, (double)item.Memory / memoryScale))
            .ToArray());

        HasTopContainers = TopCpu.Count > 0;
    }

    private void ApplyEndpoints(RuntimeSnapshot snapshot)
    {
        List<EndpointRow> publishing =
        [
            .. snapshot.Containers
                .Where(container => container.IsRunning && PublishedPort.ParseList(container.Ports).Any(port => port.HostPort is not null))
                .OrderBy(container => container.Names, StringComparer.OrdinalIgnoreCase)
                .Select(container => new EndpointRow(container.Id, container.Names, container.Ports))
        ];

        Endpoints.ReplaceWith(publishing.Take(MaxEndpoints).ToArray());
        HasEndpoints = publishing.Count > 0;
        int more = publishing.Count - MaxEndpoints;
        MoreEndpointsText = more > 0 ? $"{Humanize.Count(more, "more container")} · Containers" : "";
    }
}
