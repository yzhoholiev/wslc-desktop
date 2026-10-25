using WslcDesktop.Models;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.ViewModels;

public sealed class ContainerLogsTests
{
    private static readonly RuntimeSnapshot Snapshot = Running(Container("web"), Container("api"));

    private readonly List<(string Id, int Tail)> _logReads = [];
    private readonly List<CancellationToken> _logTokens = [];
    private readonly TestServices _test = new();
    private Func<string, Task<string>> _inspect = id => Task.FromResult($"inspect {id}");
    private Func<string, Task<string>> _logs = id => Task.FromResult($"logs of {id}");

    [Fact]
    public void Selecting_ShowsTheContainersLogsAndInspectOutput()
    {
        ContainersViewModel viewModel = Create();

        Select(viewModel, "web");

        Assert.Equal("logs of web-id-0123456789abcdef", viewModel.Logs.Text);
        Assert.Equal("inspect web-id-0123456789abcdef", viewModel.InspectText);
        Assert.Equal([("web-id-0123456789abcdef", 500)], _logReads);
    }

    [Fact]
    public void Selecting_ExplainsEmptyLogs()
    {
        _logs = _ => Task.FromResult("");
        ContainersViewModel viewModel = Create();

        Select(viewModel, "web");

        Assert.Equal("(no output)", viewModel.Logs.Text);
    }

    [Fact]
    public void Selecting_ShowsWhyLogsOrInspectFailed()
    {
        _logs = _ => Task.FromException<string>(new CliException("no logs"));
        _inspect = _ => Task.FromException<string>(new CliException("no inspect"));
        ContainersViewModel viewModel = Create();

        Select(viewModel, "web");

        Assert.Equal("no logs", viewModel.Logs.Text);
        Assert.Equal("no inspect", viewModel.InspectText);
    }

    [Fact]
    public void Deselecting_ClearsTheLogs()
    {
        ContainersViewModel viewModel = Create();
        Select(viewModel, "web");

        viewModel.Selected = null;

        Assert.Equal("", viewModel.Logs.Text);
        Assert.Equal("", viewModel.InspectText);
    }

    [Theory]
    [InlineData(double.NaN, 500)]
    [InlineData(0, 500)]
    [InlineData(42, 42)]
    public async Task RefreshLogs_ReadsTheRequestedTail(double tail, int expected)
    {
        ContainersViewModel viewModel = Create();
        Select(viewModel, "web");

        viewModel.Logs.Tail = tail;
        await viewModel.Logs.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(expected, _logReads[^1].Tail);
    }

    [Fact]
    public async Task RefreshLogs_DoesNothingWithoutASelection()
    {
        ContainersViewModel viewModel = Create();

        await viewModel.Logs.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(_logReads);
    }

    [Fact]
    public void FollowLogs_ReloadsTheLogsWithEachSnapshot()
    {
        ContainersViewModel viewModel = Create();
        Select(viewModel, "web");

        viewModel.Apply(Snapshot);
        Assert.Single(_logReads);

        viewModel.Logs.Follow = true;
        viewModel.Apply(Snapshot);
        Assert.Equal(2, _logReads.Count);
    }

    [Fact]
    public void FollowLogs_WaitsForTheLoadInProgress()
    {
        var pending = new TaskCompletionSource<string>();
        ContainersViewModel viewModel = Create();
        Select(viewModel, "web");
        _logs = _ => pending.Task;
        viewModel.Logs.Follow = true;

        viewModel.Apply(Snapshot);
        viewModel.Apply(Snapshot);

        Assert.Equal(2, _logReads.Count);
    }

    [Fact]
    public async Task Logs_FromAnOlderRequestDoNotReplaceNewerOnes()
    {
        var older = new TaskCompletionSource<string>();
        ContainersViewModel viewModel = Create();
        Select(viewModel, "web");

        _logs = _ => older.Task;
        Task olderLoad = viewModel.Logs.RefreshCommand.ExecuteAsync(null);
        _logs = _ => Task.FromResult("newer");
        await viewModel.Logs.RefreshCommand.ExecuteAsync(null);
        older.SetResult("older");
        await olderLoad;

        Assert.Equal("newer", viewModel.Logs.Text);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("api", "logs of api-id-0123456789abcdef")]
    public void Logs_OfAContainerNoLongerSelectedAreDropped(string? next, string expected)
    {
        var webLogs = new TaskCompletionSource<string>();
        _logs = id => id.StartsWith("web", StringComparison.Ordinal) ? webLogs.Task : Task.FromResult($"logs of {id}");
        ContainersViewModel viewModel = Create();
        Select(viewModel, "web");

        if (next is null)
        {
            viewModel.Selected = null;
        }
        else
        {
            Select(viewModel, next);
        }

        webLogs.SetResult("late web logs");

        Assert.Equal(expected, viewModel.Logs.Text);
    }

    [Fact]
    public async Task Logs_AreReadWithTheSelectionsTokenWhichASwitchCancels()
    {
        ContainersViewModel viewModel = Create();
        Select(viewModel, "web");
        viewModel.Logs.Follow = true;
        viewModel.Apply(Snapshot);
        await viewModel.Logs.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(3, _logTokens.Count);
        Assert.All(_logTokens, token => Assert.Equal(_logTokens[0], token));
        Assert.False(_logTokens[0].IsCancellationRequested);

        Select(viewModel, "api");

        Assert.True(_logTokens[0].IsCancellationRequested);
        Assert.False(_logTokens[^1].IsCancellationRequested);
    }

    [Fact]
    public void Logs_LeaveThePaneAloneWhenTheReadIsCancelled()
    {
        _logs = _ => Task.FromException<string>(new OperationCanceledException());
        ContainersViewModel viewModel = Create();

        Select(viewModel, "web");

        Assert.Equal("", viewModel.Logs.Text);
        Assert.Equal("inspect web-id-0123456789abcdef", viewModel.InspectText);
    }

    [Fact]
    public void FollowLogs_StopsWhenTheSelectedContainerDisappears()
    {
        ContainersViewModel viewModel = Create();
        Select(viewModel, "web");
        viewModel.Logs.Follow = true;

        viewModel.Apply(Running(Container("api")));

        Assert.Null(viewModel.Selected);
        Assert.Single(_logReads);
        Assert.Equal("", viewModel.Logs.Text);
    }

    private ContainersViewModel Create()
    {
        _test.Details.ReadLogs = (id, tail, ct) =>
        {
            _logReads.Add((id, tail));
            _logTokens.Add(ct);
            return _logs(id);
        };

        _test.Details.ReadInspect = (id, _) => _inspect(id);
        var viewModel = new ContainersViewModel(_test.Services);

        viewModel.Apply(Snapshot);
        return viewModel;
    }

    private static void Select(ContainersViewModel viewModel, string name) => viewModel.Selected = viewModel.Items.First(item => item.Name == name);
}
