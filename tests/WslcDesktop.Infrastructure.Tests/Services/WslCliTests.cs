using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class WslCliTests
{
    [Fact]
    public void ParseDistros_ReadsNamesStatesVersionsAndTheDefault()
    {
        const string Output = """
                                NAME                   STATE           VERSION
                                Ubuntu                 Running         2
                                docker-desktop         Stopped         2
                              * Ubuntu 24.04 LTS       Converting      1
                                broken
                              """;

        List<WslDistro> distros = WslCli.ParseDistros(Output.Replace("\r", "") + "\0");

        Assert.Equal(
            [
                new WslDistro("Ubuntu", "Running", "2", false),
                new WslDistro("docker-desktop", "Stopped", "2", false),
                new WslDistro("Ubuntu 24.04 LTS", "Converting", "1", true)
            ],
            distros);

        Assert.Equal([true, false, false], distros.Select(distro => distro.IsRunning));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  NAME   STATE   VERSION")]
    public void ParseDistros_ReturnsNothingWithoutDistributions(string output) => Assert.Empty(WslCli.ParseDistros(output));

    [Fact]
    public void ParseKeyValues_KeepsOnlyNonEmptyPairs()
    {
        List<KeyValueEntry> entries =
            WslCli.ParseKeyValues("WSL version: 3.0.3.0\r\nKernel version: 6.18.54.1-1\nno separator here\nEmpty:\n\nWindows version: 10.0.26300");

        Assert.Equal(
            [
                new KeyValueEntry("WSL version", "3.0.3.0"),
                new KeyValueEntry("Kernel version", "6.18.54.1-1"),
                new KeyValueEntry("Windows version", "10.0.26300")
            ],
            entries);
    }
}
