using WslcDesktop.Models;
using WslcDesktop.ViewModels;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.ViewModels;

public sealed class ReloadTriggerTests
{
    private readonly ReloadTrigger _trigger = new(inventory => inventory.Images);

    [Fact]
    public void ShouldReload_WaitsForTheFirstSample()
    {
        _trigger.Remember(RuntimeSnapshot.Empty);
        Assert.True(_trigger.AwaitsFirstSample);

        Assert.False(_trigger.ShouldReload(RuntimeSnapshot.Empty));
        Assert.True(_trigger.ShouldReload(Running()));
        Assert.False(_trigger.AwaitsFirstSample);
    }

    [Fact]
    public void ShouldReload_WhenTheRuntimeStartsOrStops()
    {
        _trigger.Remember(Stopped());

        Assert.False(_trigger.ShouldReload(Stopped()));
        Assert.True(_trigger.ShouldReload(Running()));
        Assert.False(_trigger.ShouldReload(Running()));
        Assert.True(_trigger.ShouldReload(Stopped()));
    }

    [Fact]
    public void ShouldReload_WhenWslcBecomesReachableOrUnreachable()
    {
        RuntimeSnapshot unreachable = RuntimeSnapshot.Empty with { Error = "wslc did not respond in time." };
        _trigger.Remember(Stopped());

        Assert.True(_trigger.ShouldReload(unreachable));
        Assert.True(_trigger.ShouldReload(Stopped()));
    }

    [Fact]
    public void ShouldReload_WhenTheCountChanges()
    {
        _trigger.Remember(WithImages(3));

        Assert.False(_trigger.ShouldReload(WithImages(3)));
        Assert.False(_trigger.ShouldReload(Running()));
        Assert.True(_trigger.ShouldReload(WithImages(4)));
    }

    [Fact]
    public void ShouldReload_IgnoresCountChangesDuringTheListsOwnRefresh()
    {
        _trigger.Remember(WithImages(3));

        _trigger.BeginOwnRefresh();
        Assert.False(_trigger.ShouldReload(WithImages(2)));
        _trigger.EndOwnRefresh();

        Assert.False(_trigger.ShouldReload(WithImages(2)));
        Assert.True(_trigger.ShouldReload(WithImages(1)));
    }

    [Fact]
    public void ShouldReload_StillReloadsForAStartOrStopDuringTheListsOwnRefresh()
    {
        _trigger.Remember(WithImages(3));

        _trigger.BeginOwnRefresh();

        Assert.True(_trigger.ShouldReload(Stopped()));
    }

    private static RuntimeSnapshot WithImages(int count) =>
        Running([], new Dictionary<string, ContainerStatsDto>(), new Inventory(count, 0, 0, 0, 0, 0, 0, 0));
}
