using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

public sealed partial class DashboardViewModel : PageViewModel
{
    private const int MinimumGraphSamples = 3;

    public DashboardViewModel(AppServices services)
        : base(services)
    {
        StatusText = "Connecting to wslc…";
    }

    public ObservableCollection<ContainerUsage> TopContainers { get; } = [];

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial bool IsStopped { get; set; }

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

    [ObservableProperty]
    public partial int ContainersCount { get; set; } = -1;

    [ObservableProperty]
    public partial string ContainersDetail { get; set; } = "";

    [ObservableProperty]
    public partial int ImagesCount { get; set; } = -1;

    [ObservableProperty]
    public partial string ImagesDetail { get; set; } = "";

    [ObservableProperty]
    public partial int VolumesCount { get; set; } = -1;

    [ObservableProperty]
    public partial string VolumesDetail { get; set; } = "";

    [ObservableProperty]
    public partial int NetworksCount { get; set; } = -1;

    [ObservableProperty]
    public partial string NetworksDetail { get; set; } = "";

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

    [ObservableProperty]
    public partial string StorageValue { get; set; } = "–";

    [ObservableProperty]
    public partial string StorageCaption { get; set; } = "";

    /// <summary>
    ///     How much of the used container storage the images take, from 0 to 1.
    /// </summary>
    [ObservableProperty]
    public partial double StorageImagesShare { get; set; }

    [ObservableProperty]
    public partial bool HasStorage { get; set; }

    /// <summary>
    ///     Whether the graphs have enough samples to draw a line rather than a dot.
    /// </summary>
    [ObservableProperty]
    public partial bool HasHistory { get; set; }

    [ObservableProperty]
    public partial bool HasRunningContainers { get; set; }

    [ObservableProperty]
    public partial bool HasTopContainers { get; set; }

    public void Apply(RuntimeSnapshot snapshot)
    {
        RuntimeMonitor monitor = Services.Monitor;

        UpdateBusy();
        IsRunning = snapshot.IsRunning;
        IsStopped = snapshot.Info is not null && !snapshot.IsRunning;
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

        ApplyInventory(snapshot);

        ApplyVm(snapshot.Vm);
        CpuHistory = monitor.CpuHistory;
        MemoryHistory = monitor.MemoryHistory;
        HasHistory = CpuHistory.Length >= MinimumGraphSamples;
        ApplyStorage(snapshot);

        ApplyTopContainers(snapshot);
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

    private void ApplyVm(VmMetrics? vm)
    {
        if (vm is null)
        {
            CpuValue = MemoryValue = "–";
            CpuCaption = MemoryCaption = "";
            return;
        }

        CpuValue = $"{vm.CpuPercent:0.0}%";
        CpuCaption = $"{vm.VirtualCpus} vCPUs · load {vm.LoadAverage}";
        MemoryValue = $"{vm.MemoryPercent:0.0}%";
        string swap = vm.SwapTotal > 0 ? $"swap {ByteSize.Format(vm.SwapUsed)} of {ByteSize.Format(vm.SwapTotal)}" : "no swap";
        MemoryCaption = $"{ByteSize.Format(vm.MemoryUsed)} of {ByteSize.Format(vm.MemoryTotal)} · {swap}";
    }

    private void ApplyStorage(RuntimeSnapshot snapshot)
    {
        HasStorage = snapshot.Storage is not null;
        if (snapshot.Storage is not { } storage)
        {
            StorageValue = "–";
            StorageCaption = "";
            StorageImagesShare = 0;
            return;
        }

        long images = snapshot.Inventory?.ImagesSize ?? 0;
        long unused = snapshot.Inventory?.UnusedImagesSize ?? 0;
        StorageValue = $"{ByteSize.Format(storage.Used)} used";
        StorageCaption = unused > 0
            ? $"Images {ByteSize.Format(images)} · {ByteSize.Format(unused)} in unused images"
            : $"Images {ByteSize.Format(images)}";

        StorageImagesShare = storage.Used == 0 ? 0 : Math.Min(1, (double)images / storage.Used);
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
        if (!snapshot.IsRunning)
        {
            HasRunningContainers = false;
            ContainersCount = ImagesCount = VolumesCount = NetworksCount = -1;
            ContainersDetail = ImagesDetail = VolumesDetail = NetworksDetail = "";
            return;
        }

        int running = snapshot.RunningContainers;
        HasRunningContainers = running > 0;
        ContainersCount = snapshot.Containers.Count;
        ContainersDetail = $"{running} running · {snapshot.Containers.Count - running} stopped";

        if (snapshot.Inventory is { } inventory)
        {
            ImagesCount = inventory.Images;
            ImagesDetail = $"{ByteSize.Format(inventory.ImagesSize)} · {inventory.UnusedImages} unused";
            VolumesCount = inventory.Volumes;
            VolumesDetail = $"{inventory.Volumes - inventory.AnonymousVolumes} named · {inventory.AnonymousVolumes} anonymous";
            NetworksCount = inventory.Networks;
            NetworksDetail = $"{inventory.CustomNetworks} user-defined · {inventory.Networks - inventory.CustomNetworks} built-in";
        }
    }

    private void ApplyTopContainers(RuntimeSnapshot snapshot)
    {
        var busiest = snapshot.Containers
            .Select(container => (Container: container, Stats: snapshot.Stats.GetValueOrDefault(container.Id)))
            .Where(pair => pair.Stats is not null)
            .Select(pair => (
                pair.Container,
                pair.Stats,
                Cpu: ByteSize.ParsePercent(pair.Stats!.CpuPerc),
                Memory: ByteSize.Parse(pair.Stats.MemUsed)))
            .OrderByDescending(usage => usage.Cpu)
            .ThenByDescending(usage => usage.Memory)
            .Take(6)
            .ToList();

        // Scaling CPU against at least one full core keeps a nearly idle top container from showing a full bar.
        double cpuScale = Math.Max(100, busiest.Select(usage => usage.Cpu).DefaultIfEmpty().Max());
        long memoryScale = Math.Max(1, busiest.Select(usage => usage.Memory).DefaultIfEmpty().Max());
        var top = busiest
            .Select(usage => new ContainerUsage(
                usage.Container.Names,
                usage.Container.Image,
                usage.Stats!.CpuPerc,
                usage.Cpu / cpuScale,
                ByteSize.Normalize(usage.Stats.MemUsed),
                (double)usage.Memory / memoryScale))
            .ToList();

        TopContainers.ReplaceWith(top);
        HasTopContainers = TopContainers.Count > 0;
    }
}
