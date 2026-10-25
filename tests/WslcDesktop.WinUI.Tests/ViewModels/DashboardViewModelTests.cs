using WslcDesktop.Models;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.ViewModels;

public sealed class DashboardViewModelTests
{
    private static readonly Inventory Inventory = new(
        5, 406_900_000, 1, 3, 1, 5, 2, 0);

    private readonly TestServices _test = new();

    [Fact]
    public void Apply_SummarizesTheRunningRuntime()
    {
        var viewModel = new DashboardViewModel(_test.Services);

        viewModel.Apply(Running([Container("web"), Container("batch", "exited")], new Dictionary<string, ContainerStatsDto>(), Inventory));

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.IsStopped);
        Assert.Equal($"Session {Wslc.DefaultSessionName}", viewModel.StatusText);
        Assert.Equal(2, viewModel.ContainersCount);
        Assert.Equal("1 running · 1 stopped", viewModel.ContainersDetail);
        Assert.Equal(5, viewModel.ImagesCount);
        Assert.Equal("388.1 MiB · 1 unused", viewModel.ImagesDetail);
        Assert.Equal("2 named · 1 anonymous", viewModel.VolumesDetail);
        Assert.Equal("2 user-defined · 3 built-in", viewModel.NetworksDetail);
    }

    [Theory]
    [InlineData(Wslc.NotInstalledMessage, true, "wslc is not installed")]
    [InlineData("boom", false, "Could not query wslc")]
    public void Apply_TitlesTheError(string error, bool isMissing, string expectedTitle)
    {
        var viewModel = new DashboardViewModel(_test.Services);

        viewModel.Apply(RuntimeSnapshot.Empty with { Error = error, IsWslcMissing = isMissing });

        Assert.True(viewModel.HasError);
        Assert.Equal(isMissing, viewModel.IsWslcMissing);
        Assert.Equal(expectedTitle, viewModel.ErrorTitle);
        Assert.Equal(error, viewModel.ErrorText);
    }

    [Fact]
    public void Apply_ShowsDashesWhileStopped()
    {
        var viewModel = new DashboardViewModel(_test.Services);

        viewModel.Apply(Stopped());

        Assert.True(viewModel.IsStopped);
        Assert.Equal(-1, viewModel.ContainersCount);
        Assert.Equal(-1, viewModel.ImagesCount);
        Assert.Equal("–", viewModel.CpuValue);
        Assert.Equal("–", viewModel.MemoryValue);
        Assert.Equal("–", viewModel.StorageValue);
        Assert.False(viewModel.HasStorage);
        Assert.False(viewModel.HasRunningContainers);
        Assert.Equal("The container runtime is stopped. It starts on demand when you run a container command.", viewModel.StatusText);
    }

    [Fact]
    public void Apply_NamesOtherSessionsWhenOnlyThoseRun()
    {
        var viewModel = new DashboardViewModel(_test.Services);

        viewModel.Apply(Stopped("wslc-cli-other"));

        Assert.Equal($"Session {Wslc.DefaultSessionName} is stopped. Also running, with separate containers: wslc-cli-other.", viewModel.StatusText);
    }

    [Fact]
    public async Task Apply_ShowsAStartBegunElsewhereAsBusy()
    {
        var viewModel = new DashboardViewModel(_test.Services);
        var start = new TaskCompletionSource();
        Task<bool> starting = _test.Services.Actions.RunExclusiveAsync(RuntimeActions.StartRuntimeDescription, () => start.Task);

        viewModel.Apply(Stopped());

        Assert.True(viewModel.IsBusy);
        Assert.Equal("Starting the container runtime…", viewModel.StatusText);

        start.SetResult();
        await starting;
        viewModel.Apply(Stopped());

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void Apply_ReportsVmAndStorageMetrics()
    {
        var viewModel = new DashboardViewModel(_test.Services);
        var vm = new VmMetrics(12, 4.56, 32L << 30, 24L << 30, 0, 0, "0.08  0.02  0.01",
            TimeSpan.FromSeconds(93784));

        viewModel.Apply(Running() with
        {
            Vm = vm,
            Storage = new DiskUsage(1_000_000_000_000, 5_000_000_000),
            Inventory = new Inventory(2, 1_250_000_000, 1, 0, 0, 0, 0, 500_000_000)
        });

        Assert.Equal("4.6%", viewModel.CpuValue);
        Assert.Equal("12 vCPUs · load 0.08  0.02  0.01", viewModel.CpuCaption);
        Assert.Equal("25.0%", viewModel.MemoryValue);
        Assert.Equal("8 GiB of 32 GiB · no swap", viewModel.MemoryCaption);
        Assert.Equal($"Up 1d 2h 3m · session {Wslc.DefaultSessionName}", viewModel.StatusText);
        Assert.Equal("4.7 GiB used", viewModel.StorageValue);
        Assert.Equal("Images 1.2 GiB · 476.8 MiB in unused images", viewModel.StorageCaption);
        Assert.Equal(0.25, viewModel.StorageImagesShare, Tolerance.Default);
        Assert.True(viewModel.HasStorage);
    }

    [Theory]
    [InlineData(42, "Up 42s")]
    [InlineData(150, "Up 2m")]
    [InlineData(7_500, "Up 2h 5m")]
    public void Apply_ShowsTheVmUptimeInTheStatus(int seconds, string expected)
    {
        var viewModel = new DashboardViewModel(_test.Services);

        viewModel.Apply(Running() with { Vm = new VmMetrics(1, 0, 100, 50, 0, 0, "", TimeSpan.FromSeconds(seconds)) });

        Assert.StartsWith($"{expected} · session ", viewModel.StatusText);
    }

    [Fact]
    public void Apply_ShowsGraphsOnlyOnceThereAreEnoughSamples()
    {
        var viewModel = new DashboardViewModel(_test.Services);
        RuntimeSnapshot sample = Running() with { Vm = new VmMetrics(1, 5, 100, 50, 0, 0, "", TimeSpan.Zero) };

        _test.Services.Monitor.Publish(sample);
        _test.Services.Monitor.Publish(sample);
        viewModel.Apply(sample);
        Assert.False(viewModel.HasHistory);

        _test.Services.Monitor.Publish(sample);
        viewModel.Apply(sample);
        Assert.True(viewModel.HasHistory);
    }

    [Fact]
    public void Apply_RanksTheBusiestContainersAndScalesTheirBars()
    {
        ContainerDto idle = Container("idle");
        ContainerDto busy = Container("busy");
        ContainerDto heavy = Container("heavy");
        var viewModel = new DashboardViewModel(_test.Services);

        viewModel.Apply(Running([idle, busy, heavy], new Dictionary<string, ContainerStatsDto>
        {
            [idle.Id] = Stats(idle, "0.10%", "2MiB"),
            [busy.Id] = Stats(busy, "50.00%", "8MiB"),
            [heavy.Id] = Stats(heavy, "5.00%", "32MiB")
        }));

        Assert.Equal(["busy", "heavy", "idle"], viewModel.TopContainers.Select(usage => usage.Name));
        // CPU bars are scaled against one full core, so 50% fills half the bar.
        Assert.Equal(0.5, viewModel.TopContainers[0].CpuShare, Tolerance.Default);
        Assert.Equal(1, viewModel.TopContainers[1].MemoryShare, Tolerance.Default);
        Assert.Equal(0.25, viewModel.TopContainers[0].MemoryShare, Tolerance.Default);
        Assert.True(viewModel.HasTopContainers);
    }

    [Fact]
    public void GetCleanupSummary_CountsFromTheLatestSnapshot()
    {
        _test.Services.Monitor.Publish(Running(
            [Container("web"), Container("batch", "exited"), Container("held", "paused")],
            new Dictionary<string, ContainerStatsDto>(),
            Inventory));

        CleanupSummary summary = new DashboardViewModel(_test.Services).GetCleanupSummary();

        Assert.Equal(new CleanupSummary(1, 1, 5, "388.1 MiB", 3), summary);
    }
}
