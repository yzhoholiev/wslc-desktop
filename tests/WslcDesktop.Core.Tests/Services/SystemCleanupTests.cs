using WslcDesktop.Models;
using WslcDesktop.Services;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.Services;

public sealed class SystemCleanupTests
{
    private static readonly CleanupOptions Everything = new(true, true, true, true, true, true, true);

    private readonly TestServices _test = new();

    [Theory]
    [InlineData("Deleted Containers:\nabc\n\nTotal reclaimed space: 12.5MB", 12_500_000L)]
    [InlineData("Total reclaimed space: 0B", 0L)]
    [InlineData("nothing to report", 0L)]
    public void ParseReclaimed_ReadsThePruneSummary(string output, long expected) => Assert.Equal(expected, SystemCleanup.ParseReclaimed(output));

    [Fact]
    public void HasAny_IgnoresStoppingAlone()
    {
        Assert.False(new CleanupOptions(true, false, false, false, true, false, true).HasAny);
        Assert.True(new CleanupOptions(false, false, false, false, false, true, false).HasAny);
    }

    [Fact]
    public async Task RunAsync_PrunesContainersFirstAndSumsTheReclaimedSpace()
    {
        _test.Commands.PruneOutput = "Deleted: abc\nTotal reclaimed space: 1.5MB";

        CleanupResult result = await _test.Services.Cleanup.RunAsync(Everything with { StopRunningContainers = false });

        Assert.Equal(["prune-containers", "prune-networks", "prune-images --all", "prune-volumes --all"], _test.Commands.Calls);
        Assert.Equal(6_000_000L, result.ReclaimedBytes);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public async Task RunAsync_RunsOnlyTheChosenSteps()
    {
        var options = new CleanupOptions(false, false, false, true, false, true, false);

        await _test.Services.Cleanup.RunAsync(options);

        Assert.Equal(["prune-images", "prune-volumes"], _test.Commands.Calls);
    }

    [Fact]
    public async Task RunAsync_ReportsAFailedStepAndRunsTheRest()
    {
        _test.Commands.Failing.Add("prune-images");

        CleanupResult result = await _test.Services.Cleanup.RunAsync(Everything with { StopRunningContainers = false });

        Assert.Equal(["prune-containers", "prune-networks", "prune-images --all", "prune-volumes --all"], _test.Commands.Calls);
        Assert.Equal(["images: prune-images failed"], result.Failures);
    }

    [Fact]
    public async Task RunAsync_StillPrunesWhenStoppingContainersFails()
    {
        _test.Reader.Containers = [Container("web")];
        _test.Commands.Failing.Add("stop-many");

        CleanupResult result = await _test.Services.Cleanup.RunAsync(Everything);

        Assert.Equal(["stopping containers: stop-many failed"], result.Failures);
        Assert.Contains("prune-containers", _test.Commands.Calls);
    }
}
