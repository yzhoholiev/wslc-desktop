using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class ContainerActionsTests
{
    private readonly ContainerActions _actions;
    private readonly FakeRuntimeCommands _commands = new();

    public ContainerActionsTests()
    {
        _actions = new ContainerActions(_commands);
    }

    [Theory]
    [InlineData("start", "Starting web", "Started web.", "start id1")]
    [InlineData("stop", "Stopping web", "Stopped web.", "stop id1")]
    [InlineData("restart", "Restarting web", "Restarted web.", "restart id1")]
    [InlineData("remove", "Removing web", "Removed web.", "remove id1")]
    public async Task Action_NamesTheContainerAndRunsItsCommand(string action, string description, string successMessage, string command)
    {
        UserOperation operation = action switch
        {
            "start" => _actions.Start("id1", "web"),
            "stop" => _actions.Stop("id1", "web"),
            "restart" => _actions.Restart("id1", "web"),
            _ => _actions.Remove("id1", "web")
        };

        await operation.Run();

        Assert.Equal((description, successMessage), (operation.Description, operation.SuccessMessage));
        Assert.Equal([command], _commands.Calls);
    }

    [Theory]
    [InlineData(true, "Stopping web")]
    [InlineData(false, "Starting web")]
    public void StartOrStop_StopsARunningContainerAndStartsAStoppedOne(bool isRunning, string expected) =>
        Assert.Equal(expected, _actions.StartOrStop("id1", "web", isRunning).Description);
}
