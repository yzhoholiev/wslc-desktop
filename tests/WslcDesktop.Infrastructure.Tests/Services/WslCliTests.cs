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
    public void ParseAvailableDistros_ReadsInstallNamesAndFriendlyNames()
    {
        const string Output = """
                              The following is a list of valid distributions that can be installed.
                              Install using 'wsl.exe --install <Distro>'.

                              NAME                            FRIENDLY NAME
                              Ubuntu                          Ubuntu
                              Ubuntu-24.04                    Ubuntu 24.04 LTS
                              SUSE-Linux-Enterprise-15-SP7    SUSE Linux Enterprise 15 SP7
                              OracleLinux_9_5                 Oracle Linux 9.5
                              """;

        Assert.Equal(
            [
                new AvailableDistro("Ubuntu", "Ubuntu"),
                new AvailableDistro("Ubuntu-24.04", "Ubuntu 24.04 LTS"),
                new AvailableDistro("SUSE-Linux-Enterprise-15-SP7", "SUSE Linux Enterprise 15 SP7"),
                new AvailableDistro("OracleLinux_9_5", "Oracle Linux 9.5")
            ],
            WslCli.ParseAvailableDistros(Output.ReplaceLineEndings("\r\n")));
    }

    [Fact]
    public void ParseAvailableDistros_ReadsTranslatedHeadersAndStopsAtTheEndOfTheTable()
    {
        const string Output = """

                              Voici la liste des distributions valides qui peuvent être installées.

                              NOM            NOM CONVIVIAL
                              Debian         Debian GNU/Linux
                              nameonly

                              Note           A closing remark
                              """;

        Assert.Equal(
            [new AvailableDistro("Debian", "Debian GNU/Linux"), new AvailableDistro("nameonly", "nameonly")],
            WslCli.ParseAvailableDistros(Output));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Wsl/Service/WSL_E_DISTRO_LIST_UNAVAILABLE")]
    [InlineData("NAME      FRIENDLY NAME")]
    public void ParseAvailableDistros_ReturnsNothingWithoutAList(string output) => Assert.Empty(WslCli.ParseAvailableDistros(output));

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
