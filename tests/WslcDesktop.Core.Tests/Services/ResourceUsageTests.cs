using WslcDesktop.Models;
using WslcDesktop.Services;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.Services;

public sealed class ResourceUsageTests
{
    private const string NginxId = "sha256:76090a34b858aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Theory]
    [InlineData("nginx:alpine", "", "nginx:alpine", true)]
    [InlineData("nginx", "", "nginx:latest", true)]
    [InlineData("nginx:latest", "", "nginx", true)]
    [InlineData("nginx", "", "nginx:alpine", false)]
    [InlineData("registry.local:5000/team/app", "", "registry.local:5000/team/app:latest", true)]
    [InlineData("76090a34b858", "", "something:else", true)]
    [InlineData("76090a34", "", "something:else", false)]
    [InlineData("other:tag", NginxId, "other:tag", true)]
    [InlineData("nginx:alpine", "sha256:different", "nginx:alpine", false)]
    public void IsImage_MatchesByIdWhenKnownOtherwiseByReference(string containerImage, string containerImageId, string reference, bool expected) =>
        Assert.Equal(expected, ResourceUsage.IsImage(containerImage, containerImageId, NginxId, reference));

    [Theory]
    [InlineData("demo-pgdata", "demo-pgdata", true)]
    [InlineData("demo-pgdata", "demo-pgdata2", false)]
    [InlineData("a34c20d2dacb0d…", "a34c20d2dacb0da6c4727d09", true)]
    [InlineData("a34c20d2dacb0d…", "b34c20d2dacb0da6c4727d09", false)]
    public void VolumeNameMatches_AcceptsNamesTheCliCutShort(string listed, string name, bool expected) =>
        Assert.Equal(expected, ResourceUsage.VolumeNameMatches(listed, name));

    [Fact]
    public void ContainersUsing_FindsContainersInNameOrder()
    {
        ContainerDto[] containers = new[]
        {
            Container("web", image: "nginx:alpine", networks: "demo-net", imageId: NginxId),
            Container("db", image: "postgres:16-alpine", mounts: "demo-pgdata,a34c20d2dacb0d…", networks: "demo-net,bridge"),
            Container("cache", image: "redis:7-alpine", mounts: "cache-data")
        };

        Assert.Equal(["web"], ResourceUsage.ContainersUsingImage(NginxId, "nginx:alpine", containers).Select(container => container.Names));
        Assert.Equal(["db"], ResourceUsage.ContainersUsingVolume("demo-pgdata", containers).Select(container => container.Names));
        Assert.Equal(["db"], ResourceUsage.ContainersUsingVolume("a34c20d2dacb0da6c4727d09", containers).Select(container => container.Names));
        Assert.Equal(["db", "web"], ResourceUsage.ContainersUsingNetwork("demo-net", containers).Select(container => container.Names));
        Assert.Empty(ResourceUsage.ContainersUsingNetwork("host", containers));
    }
}
