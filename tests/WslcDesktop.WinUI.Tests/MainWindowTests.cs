using WslcDesktop.Services;

namespace WslcDesktop.Tests;

public sealed class MainWindowTests
{
    [Theory]
    [InlineData("containers", LaunchCommand.ShowContainers)]
    [InlineData("images", LaunchCommand.ShowImages)]
    [InlineData("volumes", LaunchCommand.ShowVolumes)]
    [InlineData("wsl", LaunchCommand.ShowWsl)]
    [InlineData("dashboard", LaunchCommand.None)]
    [InlineData("networks", LaunchCommand.None)]
    [InlineData("settings", LaunchCommand.None)]
    public void CommandForPage_ReopensThePagesALaunchCommandCanOpen(string tag, LaunchCommand expected) =>
        Assert.Equal(expected, MainWindow.CommandForPage(tag));

    [Fact]
    public void PageFor_OpensThePageOfEveryShowCommandAndNoneOfTheOthers()
    {
        foreach (LaunchCommand command in Enum.GetValues<LaunchCommand>())
        {
            string? page = MainWindow.PageFor(command);
            Assert.Equal(command.ToString().StartsWith("Show", StringComparison.Ordinal), page is not null);
            Assert.True(page is null || MainWindow.CommandForPage(page) == command);
        }
    }
}
