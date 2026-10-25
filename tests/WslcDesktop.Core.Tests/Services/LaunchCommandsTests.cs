using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class LaunchCommandsTests
{
    [Theory]
    [InlineData(new string[0], LaunchCommand.None)]
    [InlineData(new[] { "--tray" }, LaunchCommand.None)]
    [InlineData(new[] { "--containers" }, LaunchCommand.ShowContainers)]
    [InlineData(new[] { "--IMAGES" }, LaunchCommand.ShowImages)]
    [InlineData(new[] { "--tray", "--start-runtime" }, LaunchCommand.StartRuntime)]
    [InlineData(new[] { "--cleanup", "--wsl" }, LaunchCommand.Cleanup)]
    [InlineData(new[] { "--unknown" }, LaunchCommand.None)]
    public void Parse_FindsTheFirstCommand(string[] arguments, LaunchCommand expected) => Assert.Equal(expected, LaunchCommands.Parse(arguments));
}
