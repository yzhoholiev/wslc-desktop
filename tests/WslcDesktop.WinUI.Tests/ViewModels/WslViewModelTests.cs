using WslcDesktop.Models;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Tests.ViewModels;

public sealed class WslViewModelTests
{
    private readonly TestServices _test = new();

    [Fact]
    public async Task LoadAsync_ReportsAWslThatCannotBeQueried()
    {
        _test.Wsl.QueryFailure = new CliException("wsl.exe broke");
        List<Notification> notifications = [];
        _test.Notifier.Notified += notifications.Add;
        var viewModel = new WslViewModel(_test.Services);

        await viewModel.LoadAsync();

        Assert.Equal([new Notification(NotificationSeverity.Error, "Could not query WSL", "wsl.exe broke")], notifications);
        Assert.Equal("No .wslconfig file — WSL uses its defaults.", viewModel.WslConfigText);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task OnRuntimeUpdatedAsync_ReadsDistrosDuringARuntimeOperationElsewhereAndClearsItsBusyState()
    {
        var viewModel = new WslViewModel(_test.Services);
        var cleanup = new TaskCompletionSource();
        Task<bool> running = _test.Services.Actions.RunExclusiveAsync("Cleaning up", () => cleanup.Task);
        await viewModel.LoadAsync();
        _test.Wsl.Distros = [new WslDistro("Ubuntu", "Running", "2", true)];

        await viewModel.OnRuntimeUpdatedAsync();

        Assert.True(viewModel.IsBusy);
        Assert.Equal(["Ubuntu"], viewModel.Distros.Select(distro => distro.Name));

        cleanup.SetResult();
        await running;
        await viewModel.OnRuntimeUpdatedAsync();

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task OnRuntimeUpdatedAsync_RereadsTheDistrosEveryThirdUpdate()
    {
        var viewModel = new WslViewModel(_test.Services);
        List<int> shown = [];

        for (int update = 1; update <= 4; update++)
        {
            _test.Wsl.Distros = [.. Enumerable.Range(1, update).Select(index => new WslDistro($"distro-{index}", "Stopped", "2", false))];
            await viewModel.OnRuntimeUpdatedAsync();
            shown.Add(viewModel.Distros.Count);
        }

        Assert.Equal([1, 1, 1, 4], shown);
    }

    [Fact]
    public async Task LoadAsync_ShowsTheWslConfig()
    {
        _test.WslConfig.Text = "[wsl2]\nmemory=8GB\n\n";
        var viewModel = new WslViewModel(_test.Services);

        await viewModel.LoadAsync();

        Assert.Equal("[wsl2]\nmemory=8GB", viewModel.WslConfigText);
    }
}
