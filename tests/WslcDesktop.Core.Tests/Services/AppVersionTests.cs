using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class AppVersionTests
{
    [Theory]
    [InlineData("1.1.1-10", "1.1.1-rc.1")]
    [InlineData("1.1.1-rc.1", "1.1.1-rc.2")]
    [InlineData("1.1.1-rc.2", "1.1.1-rc.10")]
    [InlineData("1.1.1-rc.1", "1.1.1-rc.1.1")]
    [InlineData("1.1.1-rc.1", "1.1.1")]
    [InlineData("1.1.0", "1.1.1-1")]
    [InlineData("1.9.0", "1.10.0")]
    [InlineData("1.9.9", "2.0.0")]
    public void CompareTo_OrdersBySemVer(string lower, string higher)
    {
        AppVersion low = Parse(lower);
        AppVersion high = Parse(higher);

        Assert.True(low < high);
        Assert.True(high > low);
        Assert.False(low >= high);
        Assert.False(high <= low);
    }

    [Fact]
    public void CompareTo_TreatsEqualVersionsAsEqual()
    {
        Assert.Equal(0, Parse("1.2.0").CompareTo(Parse("v1.2.0+build.5")));
        Assert.Equal(Parse("1.2.0"), Parse("v1.2.0+build.5"));
    }

    [Theory]
    [InlineData("v1.2.0", "1.2.0")]
    [InlineData("V1.2.0", "1.2.0")]
    [InlineData("1.2.0+Branch.main.Sha.abc", "1.2.0")]
    [InlineData("1.1.1-8+Branch.main", "1.1.1-8")]
    [InlineData(" 1.2.0-rc.1 ", "1.2.0-rc.1")]
    public void TryParse_DropsTagPrefixAndBuildMetadata(string text, string expected) => Assert.Equal(expected, Parse(text).ToString());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nightly")]
    [InlineData("v2")]
    [InlineData("1.2")]
    [InlineData("1.2.x")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-rc..1")]
    [InlineData("1.2.3.4")]
    [InlineData("-1.2.3")]
    [InlineData("1.2.99999999999999999999")]
    public void TryParse_RejectsWhatIsNotAVersion(string? text)
    {
        Assert.False(AppVersion.TryParse(text, out AppVersion? version));
        Assert.Null(version);
    }

    [Theory]
    [InlineData("1.2.0", false)]
    [InlineData("1.2.0-rc.1", true)]
    [InlineData("1.1.1-10", true)]
    public void IsPreRelease_FollowsThePreReleasePart(string text, bool expected) => Assert.Equal(expected, Parse(text).IsPreRelease);

    private static AppVersion Parse(string text) =>
        AppVersion.TryParse(text, out AppVersion? version) ? version : throw new FormatException(text);
}
