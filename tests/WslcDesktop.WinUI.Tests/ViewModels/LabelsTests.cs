using WslcDesktop.Models;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.ViewModels;

public sealed class LabelsTests
{
    private static readonly AppVersion NextVersion = FakeInstalledApp.Version("1.2.0");

    public static TheoryData<UpdateState, string> UpdateStatuses => new()
    {
        { new UpdateState(UpdateStatus.Unavailable), "Updates are available only in the released app." },
        { new UpdateState(UpdateStatus.NotChecked), "Not checked yet" },
        { new UpdateState(UpdateStatus.Checking), "Checking for updates…" },
        { new UpdateState(UpdateStatus.UpToDate), "Up to date" },
        { new UpdateState(UpdateStatus.Available, NextVersion), "Version 1.2.0 is available" },
        { new UpdateState(UpdateStatus.Downloading, NextVersion, 0.4), "Downloading… 40%" },
        { new UpdateState(UpdateStatus.RestartRequired, NextVersion), "Restart to finish updating to 1.2.0" },
        { new UpdateState(UpdateStatus.Failed, NextVersion, Message: "GitHub is not reachable."), "GitHub is not reachable." }
    };

    public static TheoryData<UpdateState, string?> UpdateMenuTexts => new()
    {
        { new UpdateState(UpdateStatus.Unavailable), null },
        { new UpdateState(UpdateStatus.NotChecked), null },
        { new UpdateState(UpdateStatus.Checking), null },
        { new UpdateState(UpdateStatus.UpToDate), null },
        { new UpdateState(UpdateStatus.Failed, Message: "GitHub is not reachable."), null },
        { new UpdateState(UpdateStatus.Available, NextVersion), "Update to 1.2.0" },
        { new UpdateState(UpdateStatus.Failed, NextVersion, Message: "No."), "Update to 1.2.0" },
        { new UpdateState(UpdateStatus.Downloading, NextVersion, 0.4), "Downloading update… 40%" },
        { new UpdateState(UpdateStatus.RestartRequired, NextVersion), "Restart to finish updating" }
    };

    [Theory]
    [MemberData(nameof(UpdateStatuses))]
    public void UpdateStatus_DescribesEveryState(UpdateState state, string expected) => Assert.Equal(expected, Labels.UpdateStatus(state));

    [Fact]
    public void UpdateStatus_CoversEveryStatus() =>
        Assert.Equal(Enum.GetValues<UpdateStatus>().Order(), UpdateStatuses.Select(row => row.Data.Item1.Status).Order());

    [Theory]
    [MemberData(nameof(UpdateMenuTexts))]
    public void UpdateMenuText_OffersWhatCanBeDone(UpdateState state, string? expected) => Assert.Equal(expected, Labels.UpdateMenuText(state));

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
