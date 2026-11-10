using WslcDesktop.Models;
using WslcDesktop.Services;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.Models;

public sealed class RuntimeSnapshotTests
{
    [Fact]
    public void IsRunning_RequiresThisAppsSession()
    {
        Assert.True(Running().IsRunning);
        Assert.False(Stopped("wslc-cli-someone-else").IsRunning);
        Assert.False(RuntimeSnapshot.Empty.IsRunning);
    }

    [Fact]
    public void IsOtherSideRunning_LooksForTheOtherElevationsSession()
    {
        Assert.True(Stopped(Wslc.OtherSessionName).IsOtherSideRunning);
        Assert.True((Running() with { Info = Info(Wslc.DefaultSessionName, Wslc.OtherSessionName) }).IsOtherSideRunning);
        Assert.False(Running().IsOtherSideRunning);
        Assert.False(Stopped("wslc-cli-someone-else").IsOtherSideRunning);
        Assert.Null(RuntimeSnapshot.Empty.IsOtherSideRunning);
    }

    [Fact]
    public void State_PutsFailuresBeforeTheSession()
    {
        Assert.Equal(RuntimeState.Connecting, RuntimeSnapshot.Empty.State);
        Assert.Equal(RuntimeState.Stopped, Stopped().State);
        Assert.Equal(RuntimeState.Running, Running().State);
        Assert.Equal(RuntimeState.Error, Running().WithError("lost").State);
        Assert.Equal(RuntimeState.Error, (RuntimeSnapshot.Empty with { IsWslcMissing = true }).State);
    }

    [Fact]
    public void SessionNames_DifferOnlyInTheAdministratorPrefix()
    {
        Assert.Equal($"wslc-cli-{Environment.UserName}", Wslc.SessionNameFor(false));
        Assert.Equal($"wslc-cli-admin-{Environment.UserName}", Wslc.SessionNameFor(true));
        Assert.NotEqual(Wslc.DefaultSessionName, Wslc.OtherSessionName);
    }

    [Fact]
    public void RunningContainers_CountsOnlyRunningOnes() =>
        Assert.Equal(2, Running(Container("a"), Container("b"), Container("c", "exited"), Container("d", "paused")).RunningContainers);
}
