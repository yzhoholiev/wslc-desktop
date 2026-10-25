using WslcDesktop.Models;
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
    public void RunningContainers_CountsOnlyRunningOnes() =>
        Assert.Equal(2, Running(Container("a"), Container("b"), Container("c", "exited"), Container("d", "paused")).RunningContainers);
}
