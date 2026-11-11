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
        Assert.True(viewModel.HasOverview);
        Assert.False(viewModel.IsConnecting);
        Assert.Equal(("1", "of 2 running"), (viewModel.ContainersCount, viewModel.ContainersText));
        Assert.Equal(("5", "388.1 MiB"), (viewModel.ImagesCount, viewModel.ImagesText));
        Assert.Equal(("3", "1 anonymous"), (viewModel.VolumesCount, viewModel.VolumesText));
        Assert.Equal(("5", "2 user-defined"), (viewModel.NetworksCount, viewModel.NetworksText));
    }

    [Fact]
    public void Apply_OffersTheFirstStepsWhileNothingIsPulledOrCreated()
    {
        var viewModel = new DashboardViewModel(_test.Services);

        viewModel.Apply(Running([], new Dictionary<string, ContainerStatsDto>(), new Inventory(0, 0, 0, 0, 0, 3, 0, 0)));

        Assert.True(viewModel.IsFirstRun);
        Assert.False(viewModel.HasOverview);
    }

    [Fact]
    public void Apply_HidesTheBannerWhileNothingNeedsAttention()
    {
        var viewModel = new DashboardViewModel(_test.Services);

        viewModel.Apply(Running(Container("web"), Container("batch", "exited")));

        Assert.False(viewModel.HasAttention);
        Assert.Null(viewModel.SingleAttention);
        Assert.False(viewModel.HasAttentionList);
    }

    [Fact]
    public void Apply_PutsASingleAttentionItemInTheBannerItself()
    {
        var viewModel = new DashboardViewModel(_test.Services);

        viewModel.Apply(Running(Failed("api")));

        Assert.True(viewModel.HasAttention);
        Assert.Equal("Critical", viewModel.AttentionTone);
        Assert.Equal(("api", " exited with code 1 · now", "Start"),
            (viewModel.SingleAttention?.Subject, viewModel.SingleAttention?.Text, viewModel.SingleAttention?.SecondaryAction));
        Assert.False(viewModel.HasAttentionList);
    }

    [Fact]
    public void Apply_TakesTheActionsOffTheBannerOnceTheOnlyItemIsGone()
    {
        var viewModel = new DashboardViewModel(_test.Services);
        viewModel.Apply(Running(Failed("api")));

        viewModel.Apply(Running(Container("api")));

        Assert.Null(viewModel.SingleAttention);
        Assert.False(viewModel.HasAttention);
    }

    [Fact]
    public void Apply_ListsAtMostFourContainersAndLeavesTheRestToTheContainersPage()
    {
        var viewModel = new DashboardViewModel(_test.Services);
        ContainerDto[] failed = [.. Enumerable.Range(1, 6).Select(number => Failed($"job-{number}"))];

        viewModel.Apply(Running(failed, new Dictionary<string, ContainerStatsDto>(), new Inventory(1, 1000, 1, 0, 0, 0, 0, 1000)));

        Assert.Equal(("Critical", "7 things need attention", "6 containers failed · 1000 B reclaimable"),
            (viewModel.AttentionTone, viewModel.AttentionTitle, viewModel.AttentionSummary));
        Assert.Equal(["job-1", "job-2", "job-3", "job-4", ""], viewModel.AttentionItems.Select(item => item.Subject));
        Assert.Equal("2 more containers · show them on the Containers page", viewModel.MoreAttentionText);
        Assert.True(viewModel.HasAttentionList);
    }

    [Fact]
    public void Dismiss_HidesTheItemAndRemembersIt()
    {
        ContainerDto api = Failed("api");
        RuntimeSnapshot snapshot = Running(api, Failed("worker"));
        _test.Services.Monitor.Publish(snapshot);
        var viewModel = new DashboardViewModel(_test.Services);
        viewModel.Apply(snapshot);

        viewModel.Dismiss(viewModel.AttentionItems.Single(item => item.Subject == "api"));

        Assert.Equal("worker", viewModel.SingleAttention?.Subject);
        Assert.True(_test.Settings.Current.DismissedAttention.ContainsKey(api.Id));
    }

    [Fact]
    public void Apply_ForgetsADismissalOnceTheContainerRunsAgain()
    {
        ContainerDto api = Failed("api");
        _test.Settings.Current.DismissedAttention[api.Id] = 0;
        var viewModel = new DashboardViewModel(_test.Services);

        viewModel.Apply(Running(Container("api")));

        Assert.Empty(_test.Settings.Current.DismissedAttention);
    }

    [Fact]
    public void ToggleAttention_OpensTheListAndRemembersIt()
    {
        var viewModel = new DashboardViewModel(_test.Services);
        viewModel.Apply(Running(Failed("api"), Failed("worker")));
        Assert.False(viewModel.IsAttentionListVisible);

        viewModel.ToggleAttentionCommand.Execute(null);

        Assert.True(viewModel.IsAttentionListVisible);
        Assert.Equal("Hide", viewModel.AttentionToggleText);
        Assert.True(_test.Settings.Current.IsAttentionOpen);
    }

    [Fact]
    public void Apply_ListsRunningContainersThatPublishPorts()
    {
        var viewModel = new DashboardViewModel(_test.Services);

        viewModel.Apply(Running(
            Container("web", ports: "0.0.0.0:8080->80/tcp"),
            Container("internal", ports: "5432/tcp"),
            Container("old", "exited", ports: "0.0.0.0:9090->90/tcp")));

        Assert.Equal(["web"], viewModel.Endpoints.Select(endpoint => endpoint.Name));
        Assert.True(viewModel.HasEndpoints);
        Assert.Equal("", viewModel.MoreEndpointsText);
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
    public void Apply_HidesTheOverviewWhileStopped()
    {
        var viewModel = new DashboardViewModel(_test.Services);

        viewModel.Apply(Stopped());

        Assert.True(viewModel.IsStopped);
        Assert.False(viewModel.HasOverview);
        Assert.False(viewModel.IsFirstRun);
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
        Assert.Equal("8 GiB", viewModel.MemoryValue);
        Assert.Equal(32d * (1L << 30), viewModel.MemoryTotal);
        Assert.Equal("of 32 GiB · no swap", viewModel.MemoryCaption);
        Assert.Equal("12 vCPUs · 32 GiB · load 0.08 · last 3 minutes", viewModel.VmSummary);
        Assert.Equal($"Up 1d 2h 3m · session {Wslc.DefaultSessionName}", viewModel.StatusText);
        Assert.Equal("4.7 GiB", viewModel.StorageValue);
        Assert.Equal("476.8 MiB reclaimable", viewModel.StorageCaption);
        Assert.Equal(("1.2 GiB", "3.5 GiB"), (viewModel.StorageImagesText, viewModel.StorageOtherText));
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
        _test.Services.Monitor.Publish(sample with { });
        viewModel.Apply(sample);
        Assert.False(viewModel.HasHistory);

        _test.Services.Monitor.Publish(sample with { });
        viewModel.Apply(sample);
        Assert.True(viewModel.HasHistory);
    }

    [Fact]
    public void Apply_RanksTheTopConsumersAndScalesTheirBars()
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

        Assert.Equal(["busy", "heavy", "idle"], viewModel.TopCpu.Select(usage => usage.Name));
        Assert.Equal(["heavy", "busy", "idle"], viewModel.TopMemory.Select(usage => usage.Name));
        // CPU bars are scaled against one full core, so 50% fills half the bar.
        Assert.Equal(0.5, viewModel.TopCpu[0].Share, Tolerance.Default);
        Assert.Equal(1, viewModel.TopMemory[0].Share, Tolerance.Default);
        Assert.Equal(0.25, viewModel.TopMemory[1].Share, Tolerance.Default);
        Assert.Equal("50.00%", viewModel.TopCpu[0].Value);
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

    private static ContainerDto Failed(string name)
    {
        ContainerDto container = Container(name, "exited");
        container.Status = "Exited (1) now";
        return container;
    }
}
