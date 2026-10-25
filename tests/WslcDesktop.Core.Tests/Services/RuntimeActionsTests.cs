using WslcDesktop.Models;
using WslcDesktop.Services;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.Services;

public sealed class RuntimeActionsTests
{
    private readonly TestServices _test = new();

    private RuntimeActions Actions => _test.Services.Actions;

    [Fact]
    public async Task RunExclusiveAsync_RefusesAndReportsASecondOperationWhileOneRuns()
    {
        List<Notification> notifications = [];
        _test.Notifier.Notified += notifications.Add;
        var first = new TaskCompletionSource();
        bool secondRan = false;

        Task<bool> running = Actions.RunExclusiveAsync("First", () => first.Task);
        bool started = await Actions.RunExclusiveAsync("Second", () =>
        {
            secondRan = true;
            return Task.CompletedTask;
        });

        first.SetResult();

        Assert.False(started);
        Assert.False(secondRan);
        Assert.True(await running);
        Assert.Equal([new Notification(NotificationSeverity.Error, "Second did not start", "Another operation on the runtime is still running.")],
            notifications);
    }

    [Fact]
    public async Task RunExclusiveAsync_PublishesTheOperationWhileItRuns()
    {
        List<string?> published = [];
        _test.Services.Monitor.Updated += () => published.Add(_test.Services.Monitor.Operation);
        var first = new TaskCompletionSource();

        Task<bool> running = Actions.RunExclusiveAsync("First", () => first.Task);
        string? during = _test.Services.Monitor.Operation;
        first.SetResult();
        await running;

        Assert.Equal("First", during);
        Assert.Null(_test.Services.Monitor.Operation);
        Assert.Equal(["First", null], published);
    }

    [Fact]
    public async Task StartRuntimeInBackgroundAsync_ReportsAFailedStart()
    {
        List<Notification> notifications = [];
        _test.Notifier.Notified += notifications.Add;
        _test.Commands.Failing.Add("session-start");

        await Actions.StartRuntimeInBackgroundAsync();

        Assert.Equal([new Notification(NotificationSeverity.Error, "Starting the container runtime failed", "session-start failed")], notifications);
    }

    [Fact]
    public async Task RunExclusiveAsync_AllowsTheNextOperationAfterOneFails()
    {
        await Assert.ThrowsAsync<CliException>(() => Actions.RunExclusiveAsync("Failing", () => throw new CliException("boom")));

        Assert.True(await Actions.RunExclusiveAsync("Next", () => Task.CompletedTask));
    }

    [Fact]
    public async Task StartRuntimeAsync_StartsTheDefaultSessionAndRefreshes()
    {
        await Actions.StartRuntimeAsync();

        Assert.Equal(["session-start"], _test.Commands.Calls);
        Assert.Equal(1, _test.Details.InfoReads);
    }

    [Fact]
    public async Task StopAllContainersAsync_StopsOnlyTheRunningContainers()
    {
        ContainerDto web = Container("web");
        ContainerDto api = Container("api");
        _test.Reader.Containers = [web, Container("batch", "exited"), api];

        await Actions.StopAllContainersAsync();

        Assert.Equal([$"stop-many {web.Id},{api.Id}"], _test.Commands.Calls);
        Assert.Equal(1, _test.Details.InfoReads);
    }

    [Fact]
    public async Task StopAllContainersAsync_RefreshesEvenWhenStoppingFails()
    {
        _test.Reader.Containers = [Container("web")];
        _test.Commands.Failing.Add("stop-many");

        await Assert.ThrowsAsync<CliException>(Actions.StopAllContainersAsync);

        Assert.Equal(1, _test.Details.InfoReads);
    }

    [Fact]
    public async Task StopRuntimeAsync_DisconnectsTheShellAndTerminatesTheDefaultSession()
    {
        await Actions.StopRuntimeAsync();

        Assert.NotEqual(0, _test.Session.Disconnects);
        Assert.Equal([$"session-terminate {Wslc.DefaultSessionName}"], _test.Commands.Calls);
        Assert.Equal(1, _test.Details.InfoReads);
    }

    [Fact]
    public async Task StopRuntimeOnExitAsync_LeavesAStoppedRuntimeAlone()
    {
        _test.Services.Monitor.Publish(Stopped());

        await Actions.StopRuntimeOnExitAsync();

        Assert.Empty(_test.Commands.Calls);
        Assert.Equal(0, _test.Session.Disconnects);
    }

    [Fact]
    public async Task StopRuntimeOnExitAsync_TerminatesARunningRuntimeWithoutThrowing()
    {
        _test.Services.Monitor.Publish(Running());
        _test.Commands.Failing.Add("session-terminate");

        await Actions.StopRuntimeOnExitAsync();

        Assert.Equal([$"session-terminate {Wslc.DefaultSessionName}"], _test.Commands.Calls);
        Assert.Equal(1, _test.Session.Disconnects);
    }

    [Fact]
    public async Task ShutdownWslAsync_ShutsDownWslAndRefreshes()
    {
        await Actions.ShutdownWslAsync();

        Assert.Equal(["shutdown"], _test.Wsl.Calls);
        Assert.Equal(1, _test.Details.InfoReads);
    }

    [Fact]
    public async Task ShutdownWslAsync_RefusesASecondShutdownWhileOneRuns()
    {
        _test.Wsl.PendingShutdown = new TaskCompletionSource();
        Task first = Actions.ShutdownWslAsync();

        CliException refused = await Assert.ThrowsAsync<CliException>(Actions.ShutdownWslAsync);
        _test.Wsl.PendingShutdown.SetResult();
        await first;

        Assert.Equal("WSL is already shutting down.", refused.Message);
        Assert.Equal(["shutdown"], _test.Wsl.Calls);
    }

    [Theory]
    [InlineData("", "WSL is up to date.")]
    [InlineData("  Installing WSL 2.6.1\n", "Installing WSL 2.6.1")]
    public async Task UpdateWslAsync_ReportsWhatWslPrinted(string output, string expected)
    {
        _test.Wsl.UpdateOutput = output;
        List<Notification> notifications = [];
        _test.Notifier.Notified += notifications.Add;

        await Actions.UpdateWslAsync(true);

        Assert.Equal(["update --pre-release"], _test.Wsl.Calls);
        Assert.Equal([new Notification(NotificationSeverity.Success, "Done", expected)], notifications);
    }
}
