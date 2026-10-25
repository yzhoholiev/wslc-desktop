using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class CliRuntimeReaderTests
{
    [Fact]
    public void SessionArguments_NameTheDefaultSessionSoWslcCannotBootAStoppedOne() =>
        Assert.Equal(
            ["--session", Wslc.DefaultSessionName, "system", "session", "run", "df", "-B1", "/var/lib/docker"],
            CliRuntimeReader.SessionArguments(SessionCommands.StorageUsage));

    [Fact]
    public void QueryArguments_NameTheDefaultSessionSoWslcCannotBootAStoppedOne() =>
        Assert.Equal(
            ["--session", Wslc.DefaultSessionName, "list", "--all"],
            CliRuntimeReader.QueryArguments(["list", "--all"]));

    [Fact]
    public void ParseImageConfig_ReadsTheDefaultsAContainerStartsWith()
    {
        const string Output = """
                              [{"Id":"sha256:76090a34","Config":{"Cmd":["nginx","-g","daemon off;"],"Entrypoint":["/docker-entrypoint.sh"],
                                "ExposedPorts":{"80/tcp":{},"443/tcp":{}},"Volumes":{"/data":{}},"User":"","WorkingDir":"/"}}]
                              """;

        ImageConfigDto config = CliRuntimeReader.ParseImageConfig(Output);

        Assert.Equal(["nginx", "-g", "daemon off;"], config.Cmd!);
        Assert.Equal(["/docker-entrypoint.sh"], config.Entrypoint!);
        Assert.Equal(["80/tcp", "443/tcp"], config.ExposedPorts!.Keys);
        Assert.Equal(["/data"], config.Volumes!.Keys);
        Assert.Equal("/", config.WorkingDir);
        Assert.Equal("", config.User);
    }

    [Fact]
    public void ParseImageConfig_ToleratesMissingAndNullSections()
    {
        ImageConfigDto config = CliRuntimeReader.ParseImageConfig("""[{"Config":{"Cmd":null,"ExposedPorts":null,"Volumes":null}}]""");

        Assert.Null(config.Cmd);
        Assert.Null(config.ExposedPorts);
        Assert.Null(CliRuntimeReader.ParseImageConfig("[]").Cmd);
    }
}
