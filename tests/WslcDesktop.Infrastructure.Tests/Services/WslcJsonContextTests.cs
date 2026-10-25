using WslcDesktop.Models;

namespace WslcDesktop.Tests.Services;

public sealed class WslcJsonContextTests
{
    [Fact]
    public void ParseList_ReadsOneObjectPerLine()
    {
        const string Output = """
                              {"ID":"a1","Names":"demo-web","State":"running","Ports":"127.0.0.1:8080->80/tcp","Mounts":"demo-pgdata"}

                              not json
                              {"ID":"b2","Names":"demo-db","State":"exited"}
                              """;

        List<ContainerDto> containers = WslcJsonContext.ParseList(Output, WslcJsonContext.Default.ContainerDto, WslcJsonContext.Default.ListContainerDto);

        Assert.Equal(["demo-web", "demo-db"], containers.Select(container => container.Names));
        Assert.Equal("127.0.0.1:8080->80/tcp", containers[0].Ports);
        Assert.Equal("demo-pgdata", containers[0].Mounts);
        Assert.True(containers[0].IsRunning);
        Assert.False(containers[1].IsActive);
    }

    [Fact]
    public void ParseList_ReadsAJsonArray()
    {
        List<ImageDto> images = WslcJsonContext.ParseList("""[{"Repository":"nginx","Tag":"alpine","Containers":"1"}]""", WslcJsonContext.Default.ImageDto,
            WslcJsonContext.Default.ListImageDto);

        Assert.Equal("nginx", Assert.Single(images).Repository);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n ")]
    public void ParseList_ReturnsNothingForEmptyOutput(string output) =>
        Assert.Empty(WslcJsonContext.ParseList(output, WslcJsonContext.Default.ContainerDto, WslcJsonContext.Default.ListContainerDto));
}
