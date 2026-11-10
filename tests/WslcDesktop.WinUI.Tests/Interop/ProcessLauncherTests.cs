using WslcDesktop.Interop;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Interop;

public sealed class ProcessLauncherTests
{
    [Fact]
    public void CommandLine_QuotesThePathAndAppendsSwitches()
    {
        Assert.Equal(@"""C:\Program Files\WSLC Desktop\WslcDesktop.exe""", ProcessLauncher.CommandLine(@"C:\Program Files\WSLC Desktop\WslcDesktop.exe", []));
        Assert.Equal(@"""C:\Apps\WslcDesktop.exe"" --containers", ProcessLauncher.CommandLine(@"C:\Apps\WslcDesktop.exe", ["--containers"]));
    }

    [Theory]
    [InlineData("--a b")]
    [InlineData("--a\"b")]
    [InlineData("")]
    public void CommandLine_RejectsArgumentsThatWouldNeedQuoting(string argument) =>
        Assert.Throws<ArgumentException>(() => ProcessLauncher.CommandLine(@"C:\Apps\WslcDesktop.exe", [argument]));

    [Fact]
    public void CommandLine_RejectsAPathWithAQuote() =>
        Assert.Throws<ArgumentException>(() => ProcessLauncher.CommandLine(@"C:\Apps\Wslc""Desktop.exe", []));

    [Fact]
    public void CommandLine_AcceptsEveryLaunchCommandArgument()
    {
        foreach (LaunchCommand command in Enum.GetValues<LaunchCommand>().Where(command => command != LaunchCommand.None))
        {
            Assert.EndsWith(LaunchCommands.ArgumentOf(command), ProcessLauncher.CommandLine(@"C:\Apps\WslcDesktop.exe", [LaunchCommands.ArgumentOf(command)]));
        }
    }
}
