using WslcDesktop.Models;

namespace WslcDesktop.Tests.Models;

public sealed class WslcDtosTests
{
    [Theory]
    [InlineData("running", true, true)]
    [InlineData("paused", false, true)]
    [InlineData("restarting", false, true)]
    [InlineData("exited", false, false)]
    [InlineData("created", false, false)]
    public void ContainerState_DistinguishesRunningFromActive(string state, bool running, bool active)
    {
        var container = new ContainerDto { State = state };

        Assert.Equal(running, container.IsRunning);
        Assert.Equal(active, container.IsActive);
    }

    [Fact]
    public void VolumeAndNetworkHelpers_RecognizeAnonymousAndBuiltIn()
    {
        Assert.True(new VolumeDto { Labels = "com.docker.volume.anonymous=,demo=1" }.IsAnonymous);
        Assert.False(new VolumeDto { Labels = "demo=1" }.IsAnonymous);
        Assert.True(NetworkDto.IsBuiltInName("bridge"));
        Assert.True(NetworkDto.IsBuiltInName("host"));
        Assert.True(NetworkDto.IsBuiltInName("none"));
        Assert.False(NetworkDto.IsBuiltInName("demo-net"));
    }

    [Theory]
    [InlineData(null, new[] { "/bin/sh" }, true)]
    [InlineData(null, new[] { "sh" }, true)]
    [InlineData(null, new[] { "/bin/bash" }, true)]
    [InlineData(new[] { "/bin/ash" }, null, true)]
    [InlineData(null, new[] { "sh", "-c", "sleep infinity" }, false)]
    [InlineData(new[] { "/bin/sh", "-c" }, new[] { "nginx" }, false)]
    [InlineData(new[] { "/docker-entrypoint.sh" }, new[] { "nginx", "-g", "daemon off;" }, false)]
    [InlineData(new[] { "docker-entrypoint.sh" }, new[] { "redis-server" }, false)]
    [InlineData(null, null, false)]
    public void StartsShell_IsTrueOnlyForABareShell(string[]? entrypoint, string[]? cmd, bool expected) =>
        Assert.Equal(expected, new ImageConfigDto { Entrypoint = entrypoint, Cmd = cmd }.StartsShell);
}
