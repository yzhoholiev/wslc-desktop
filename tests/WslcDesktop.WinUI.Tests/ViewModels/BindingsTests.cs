using WslcDesktop.ViewModels;

namespace WslcDesktop.Tests.ViewModels;

public sealed class BindingsTests
{
    [Theory]
    [InlineData("running", "Success")]
    [InlineData("paused", "Caution")]
    [InlineData("restarting", "Caution")]
    [InlineData("dead", "Critical")]
    [InlineData("exited", "Neutral")]
    [InlineData("created", "Neutral")]
    public void StateTone_NamesTheStylesAStateIsShownWith(string state, string expected) => Assert.Equal(expected, Bindings.StateTone(state));

    [Theory]
    [InlineData(0.25, 0.25, 0.75)]
    [InlineData(-1, 0, 1)]
    [InlineData(2, 1, 0)]
    public void FilledAndRemaining_SplitABarBetweenZeroAndOne(double fraction, double filled, double remaining)
    {
        Assert.Equal(filled, Bindings.Filled(fraction).Value, Tolerance.Default);
        Assert.Equal(remaining, Bindings.Remaining(fraction).Value, Tolerance.Default);
        Assert.True(Bindings.Filled(fraction).IsStar);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void CanAct_NeedsARunningRuntimeAndNoOtherWork(bool isRunning, bool isBusy, bool expected) =>
        Assert.Equal(expected, Bindings.CanAct(isRunning, isBusy));

    [Theory]
    [InlineData(EmptyReason.RuntimeStopped, false, true)]
    [InlineData(EmptyReason.RuntimeStopped, true, false)]
    [InlineData(EmptyReason.NoMatches, true, true)]
    [InlineData(EmptyReason.NoneRunning, true, true)]
    public void CanRunEmptyAction_WaitsForOtherWorkOnlyToStartTheRuntime(EmptyReason reason, bool isBusy, bool expected) =>
        Assert.Equal(expected, Bindings.CanRunEmptyAction(new EmptyText(reason, "", ""), isBusy));
}
