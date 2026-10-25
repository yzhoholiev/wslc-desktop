using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class HumanizeTests
{
    [Theory]
    [InlineData(0, "0 containers")]
    [InlineData(1, "1 container")]
    [InlineData(2, "2 containers")]
    public void Count_PluralizesWithS(int count, string expected) => Assert.Equal(expected, Humanize.Count(count, "container"));

    [Theory]
    [InlineData(30, "Less than a minute ago")]
    [InlineData(60, "1 minute ago")]
    [InlineData(5 * 60, "5 minutes ago")]
    [InlineData(60 * 60, "1 hour ago")]
    [InlineData(47 * 3600, "47 hours ago")]
    [InlineData(3 * 86400, "3 days ago")]
    [InlineData(21 * 86400, "3 weeks ago")]
    [InlineData(90 * 86400, "3 months ago")]
    [InlineData(800 * 86400, "2 years ago")]
    public void Ago_MatchesTheDockerCliWording(int secondsAgo, string expected) =>
        Assert.Equal(expected, Humanize.Ago(DateTimeOffset.UtcNow.AddSeconds(-secondsAgo - 1)));
}
