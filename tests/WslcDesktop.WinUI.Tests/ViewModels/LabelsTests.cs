using WslcDesktop.ViewModels;

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
}
