using WslcDesktop.Models;
using WslcDesktop.ViewModels;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.ViewModels;

public sealed class ContainersViewModelTests
{
    private static readonly RuntimeSnapshot Snapshot = Running(
        Container("web"),
        Container("batch", "exited"),
        Container("api", image: "nginx:alpine"),
        Container("paused-one", "paused"));

    private readonly TestServices _test = new();

    [Fact]
    public void Apply_ListsRunningContainersFirstThenByName()
    {
        var viewModel = new ContainersViewModel(_test.Services);

        viewModel.Apply(Snapshot);

        Assert.Equal(["api", "web", "paused-one", "batch"], viewModel.Items.Select(item => item.Name));
        Assert.Equal("2 running · 4 total", viewModel.Summary);
        Assert.True(viewModel.HasRunning);
        Assert.False(viewModel.IsEmpty);
        Assert.Equal(EmptyReason.None, viewModel.Empty.Reason);
    }

    [Fact]
    public void Apply_DropsContainersThatDisappeared()
    {
        var viewModel = new ContainersViewModel(_test.Services);
        viewModel.Apply(Snapshot);

        viewModel.Apply(Running(Container("web")));

        Assert.Equal(["web"], viewModel.Items.Select(item => item.Name));
    }

    [Fact]
    public void SearchAndShowStopped_FilterTheList()
    {
        var viewModel = new ContainersViewModel(_test.Services);
        viewModel.Apply(Snapshot);

        viewModel.SearchText = "nginx";
        Assert.Equal(["api"], viewModel.Items.Select(item => item.Name));

        viewModel.SearchText = "";
        viewModel.ShowStopped = false;
        Assert.Equal(["api", "web"], viewModel.Items.Select(item => item.Name));
    }

    [Fact]
    public void Sort_ReordersTheListByTheChosenColumn()
    {
        var viewModel = new ContainersViewModel(_test.Services);
        viewModel.Apply(Snapshot);

        viewModel.Sort.ChangeCommand.Execute("name");

        Assert.Equal(["api", "batch", "paused-one", "web"], viewModel.Items.Select(item => item.Name));
    }

    [Fact]
    public void Sort_ByCpuStartsWithTheBusiest()
    {
        ContainerDto web = Container("web");
        ContainerDto api = Container("api");
        var viewModel = new ContainersViewModel(_test.Services);
        viewModel.Apply(Running([web, api], new Dictionary<string, ContainerStatsDto>
        {
            [web.Id] = Stats(web, "0.50%", "10MiB"),
            [api.Id] = Stats(api, "49.90%", "2MiB")
        }));

        viewModel.Sort.ChangeCommand.Execute("cpu");
        Assert.Equal(["api", "web"], viewModel.Items.Select(item => item.Name));

        viewModel.Sort.ChangeCommand.Execute("memory");
        Assert.Equal(["web", "api"], viewModel.Items.Select(item => item.Name));
    }

    [Fact]
    public void Empty_ExplainsWhyNothingIsListed()
    {
        var viewModel = new ContainersViewModel(_test.Services);

        viewModel.Apply(RuntimeSnapshot.Empty);
        Assert.True(viewModel.IsEmpty);
        Assert.Equal(EmptyReason.None, viewModel.Empty.Reason);

        viewModel.Apply(Stopped());
        Assert.Equal(EmptyReason.RuntimeStopped, viewModel.Empty.Reason);

        viewModel.Apply(RuntimeSnapshot.Empty with { Error = "wslc did not respond in time." });
        Assert.Equal(EmptyReason.Unavailable, viewModel.Empty.Reason);

        viewModel.Apply(Running());
        Assert.Equal(EmptyReason.NoItems, viewModel.Empty.Reason);
        Assert.Equal("Go to images", viewModel.Empty.ActionText);

        viewModel.Apply(Running(Container("batch", "exited")));
        viewModel.ShowStopped = false;
        Assert.Equal(EmptyReason.NoneRunning, viewModel.Empty.Reason);
        Assert.Equal("1 stopped container hidden.", viewModel.Empty.Message);

        viewModel.ShowStopped = true;
        viewModel.SearchText = "zzz";
        Assert.Equal(EmptyReason.NoMatches, viewModel.Empty.Reason);
    }

    [Fact]
    public async Task RunEmptyActionAsync_ShowsHiddenStoppedContainers()
    {
        var viewModel = new ContainersViewModel(_test.Services);
        viewModel.Apply(Running(Container("batch", "exited")));
        viewModel.ShowStopped = false;

        await viewModel.RunEmptyActionAsync();

        Assert.True(viewModel.ShowStopped);
        Assert.Equal(["batch"], viewModel.Items.Select(item => item.Name));
    }

    [Fact]
    public void ShowLogTimestamps_IsRememberedInSettings()
    {
        var viewModel = new ContainersViewModel(_test.Services) { Logs = { ShowTimestamps = false } };

        Assert.False(_test.Settings.Current.ShowLogTimestamps);
        Assert.False(new ContainersViewModel(_test.Services).Logs.ShowTimestamps);

        viewModel.Logs.ShowTimestamps = true;
        Assert.True(_test.Settings.Current.ShowLogTimestamps);
    }
}
