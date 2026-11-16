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

    [Fact]
    public async Task RemoveAsync_UnregistersTheDistroAndReloadsTheList()
    {
        var ubuntu = new WslDistro("Ubuntu", "Stopped", "2", true);
        _test.Wsl.Distros = [ubuntu];
        var viewModel = new WslViewModel(_test.Services);
        await viewModel.LoadAsync();
        List<Notification> notifications = [];
        _test.Notifier.Notified += notifications.Add;

        _test.Wsl.Distros = [];
        await viewModel.RemoveAsync(ubuntu);

        Assert.Equal(["unregister Ubuntu"], _test.Wsl.Calls);
        Assert.Equal("Ubuntu removed.", Assert.Single(notifications).Message);
        Assert.Empty(viewModel.Distros);
        Assert.True(viewModel.HasNoDistros);
    }

    [Fact]
    public async Task RemoveAsync_ReportsAFailureAndKeepsTheDistro()
    {
        var ubuntu = new WslDistro("Ubuntu", "Stopped", "2", true);
        _test.Wsl.Distros = [ubuntu];
        _test.Wsl.UnregisterFailure = new CliException("access denied");
        var viewModel = new WslViewModel(_test.Services);
        await viewModel.LoadAsync();
        List<Notification> notifications = [];
        _test.Notifier.Notified += notifications.Add;

        await viewModel.RemoveAsync(ubuntu);

        Assert.Equal("Removing Ubuntu failed", Assert.Single(notifications).Title);
        Assert.Equal([ubuntu], viewModel.Distros);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task RemoveAsync_DoesNotStartWhileAnotherRuntimeOperationRuns()
    {
        var ubuntu = new WslDistro("Ubuntu", "Stopped", "2", true);
        var viewModel = new WslViewModel(_test.Services);
        var update = new TaskCompletionSource();
        Task<bool> running = _test.Services.Actions.RunExclusiveAsync("Updating WSL", () => update.Task);
        List<Notification> notifications = [];
        _test.Notifier.Notified += notifications.Add;

        await viewModel.RemoveAsync(ubuntu);

        Assert.Empty(_test.Wsl.Calls);
        Assert.Equal("Removing Ubuntu did not start", Assert.Single(notifications).Title);

        update.SetResult();
        await running;
    }

    [Fact]
    public async Task ListAvailableAsync_MarksTheDistrosAlreadyInstalled()
    {
        _test.Wsl.Distros = [new WslDistro("ubuntu", "Stopped", "2", true)];
        _test.Wsl.Available = [new AvailableDistro("Ubuntu", "Ubuntu"), new AvailableDistro("Debian", "Debian GNU/Linux")];
        var viewModel = new WslViewModel(_test.Services);

        AvailableDistroItem[] available = await viewModel.ListAvailableAsync(CancellationToken.None);

        Assert.Equal(
            [new AvailableDistroItem("Ubuntu", "Ubuntu", true), new AvailableDistroItem("Debian", "Debian GNU/Linux", false)],
            available);
    }

    [Fact]
    public async Task ListAvailableAsync_KeepsTheDistrosThePageShowsWhenWslCannotListThemAgain()
    {
        _test.Wsl.Distros = [new WslDistro("Debian", "Stopped", "2", true)];
        _test.Wsl.Available = [new AvailableDistro("Debian", "Debian GNU/Linux")];
        var viewModel = new WslViewModel(_test.Services);
        await viewModel.LoadAsync();

        // A failed wsl --list reads as no distributions.
        _test.Wsl.Distros = [];

        Assert.True(Assert.Single(await viewModel.ListAvailableAsync(CancellationToken.None)).IsInstalled);
    }

    [Fact]
    public void Install_OpensATerminalThatInstallsTheDistro()
    {
        var viewModel = new WslViewModel(_test.Services);

        viewModel.Install(new AvailableDistroItem("Debian", "Debian GNU/Linux", false));

        Assert.Equal(["install-distro Debian"], _test.Shell.Opened);
    }
}
