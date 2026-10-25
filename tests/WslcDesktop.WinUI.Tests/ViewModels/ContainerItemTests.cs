using WslcDesktop.Models;
using WslcDesktop.ViewModels;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.ViewModels;

public sealed class ContainerItemTests
{
    [Fact]
    public void Update_CopiesStateAndStats()
    {
        ContainerDto container = Container("web", ports: "127.0.0.1:8080->80/tcp");
        var item = new ContainerItem(container);

        item.Update(container, Stats(container, "1.50%", "12MiB"));

        Assert.Equal("web-id-01234", item.ShortId);
        Assert.Equal("running", item.State);
        Assert.True(item.IsRunning);
        Assert.Equal("127.0.0.1:8080->80/tcp", item.Ports);
        Assert.Equal("1.50%", item.Cpu);
        Assert.Equal("12 MiB", item.Memory);
    }

    [Fact]
    public void Update_ShowsDashesWithoutStats()
    {
        ContainerDto container = Container("web", "exited");
        var item = new ContainerItem(container);

        item.Update(container, null);

        Assert.Equal("—", item.Cpu);
        Assert.Equal("—", item.Memory);
        Assert.False(item.IsRunning);
    }

    [Fact]
    public void Update_FillsInWhatTheCliLeftOut()
    {
        ContainerDto fromCli = Container("web", mounts: "data,cach…");
        var item = new ContainerItem(fromCli);

        item.Update(Container("web", mounts: "data,cache", imageId: "sha256:abc"), null);

        Assert.Equal("sha256:abc", item.ImageId);
        Assert.Equal("data,cache", item.Mounts);
    }

    [Theory]
    [InlineData("running", 0)]
    [InlineData("paused", 1)]
    [InlineData("restarting", 1)]
    [InlineData("exited", 2)]
    [InlineData("created", 2)]
    public void StateRank_OrdersActiveFirst(string state, int rank)
    {
        var item = new ContainerItem(Container("x"));
        item.Update(Container("x", state), null);

        Assert.Equal(rank, item.StateRank);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("WEB", true)]
    [InlineData("nginx", true)]
    [InlineData("demo-web-id", true)]
    [InlineData("id-0123", false)]
    [InlineData("postgres", false)]
    public void Matches_SearchesNameImageAndIdPrefix(string search, bool expected) =>
        Assert.Equal(expected, new ContainerItem(Container("demo-web", image: "nginx:alpine")).Matches(search));
}
