using WslcDesktop.ViewModels;

namespace WslcDesktop.Tests.ViewModels;

public sealed class EmptyTextTests
{
    [Fact]
    public void Factories_DescribeEachReason()
    {
        Assert.Equal(EmptyReason.None, EmptyText.None.Reason);

        var unavailable = EmptyText.Unavailable("wslc did not respond in time.", false);
        Assert.Equal(EmptyReason.Unavailable, unavailable.Reason);
        Assert.Equal("wslc did not respond in time.", unavailable.Message);
        Assert.Equal("", unavailable.ActionText);

        var stopped = EmptyText.RuntimeStopped("images");
        Assert.Equal(EmptyReason.RuntimeStopped, stopped.Reason);
        Assert.Contains("your images", stopped.Message);
        Assert.Equal("Start runtime", stopped.ActionText);

        var noMatches = EmptyText.NoMatches("zzz");
        Assert.Equal(EmptyReason.NoMatches, noMatches.Reason);
        Assert.Equal("Nothing matches \"zzz\".", noMatches.Message);
        Assert.Equal("Clear search", noMatches.ActionText);
    }
}
