using WslcDesktop.Models;
using WslcDesktop.ViewModels;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.ViewModels;

public sealed class LabelsTests
{
    [Theory]
    [InlineData("running", "Running")]
    [InlineData("exited", "Exited")]
    [InlineData("", "")]
    public void StateLabel_Capitalizes(string state, string expected) => Assert.Equal(expected, Labels.StateLabel(state));

    [Theory]
    [InlineData(1, "In use", "Used by 1 container")]
    [InlineData(4, "In use · 4", "Used by 4 containers")]
    [InlineData(0, "In use · 0", "Unused")]
    public void UsageLabels_CountContainers(int count, string inUse, string usage)
    {
        Assert.Equal(inUse, Labels.InUseLabel(count));
        Assert.Equal(usage, Labels.UsageLabel(count));
    }

    [Theory]
    [InlineData(true, "Stop")]
    [InlineData(false, "Start")]
    public void StartStopLabel_NamesTheNextAction(bool isRunning, string expected) => Assert.Equal(expected, Labels.StartStopLabel(isRunning));

    [Theory]
    [InlineData(false, "Running · no containers", "User runtime: Running · no containers")]
    [InlineData(true, "Starting the container runtime…", "Administrator runtime: Starting the container runtime…")]
    public void CurrentSideTooltip_NamesTheSideAndItsStatus(bool isAdministrator, string status, string expected) =>
        Assert.Equal(expected, Labels.CurrentSideTooltip(isAdministrator, status));

    [Fact]
    public void RuntimeStatus_DescribesEachState()
    {
        Assert.Equal("Connecting…", Labels.RuntimeStatus(RuntimeSnapshot.Empty));
        Assert.Equal("Runtime stopped", Labels.RuntimeStatus(Stopped()));
        Assert.Equal("Running · no containers", Labels.RuntimeStatus(Running()));
        Assert.Equal("Running · 1 of 2 containers up", Labels.RuntimeStatus(Running(Container("a"), Container("b", "exited"))));
        Assert.Equal("wslc unavailable", Labels.RuntimeStatus(Running().WithError("lost")));
        Assert.Equal("wslc not installed", Labels.RuntimeStatus(RuntimeSnapshot.Empty with { IsWslcMissing = true }));
    }

    [Theory]
    [InlineData(true, true, "Administrator runtime: running\nClick to reopen as Administrator (asks for permission)")]
    [InlineData(true, false, "Administrator runtime: stopped\nClick to reopen as Administrator (asks for permission)")]
    [InlineData(false, true, "User runtime: running\nClick to reopen as User")]
    [InlineData(false, null, "User runtime: status unknown\nClick to reopen as User")]
    public void OtherSideTooltip_SaysWhatAClickDoes(bool isAdministrator, bool? isRunning, string expected) =>
        Assert.Equal(expected, Labels.OtherSideTooltip(isAdministrator, isRunning));

    [Theory]
    [InlineData(true, "Reopen as Administrator…")]
    [InlineData(false, "Reopen as User")]
    public void ReopenLabel_NamesTheSideAsTheSwitchDoes(bool isAdministrator, string expected) =>
        Assert.Equal(expected, Labels.ReopenLabel(isAdministrator));
}
