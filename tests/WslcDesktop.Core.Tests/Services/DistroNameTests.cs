using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class DistroNameTests
{
    [Theory]
    [InlineData("Ubuntu")]
    [InlineData("Ubuntu-24.04")]
    [InlineData("OracleLinux_9_5")]
    [InlineData("kali-linux")]
    public void IsValid_AcceptsTheNamesWslOffers(string name) => Assert.True(DistroName.IsValid(name));

    [Theory]
    [InlineData("")]
    [InlineData("--help")]
    [InlineData("-d")]
    [InlineData("Ubuntu 24.04")]
    [InlineData("Ubuntu;calc")]
    [InlineData("Ubuntu\"")]
    [InlineData("a&b")]
    public void IsValid_RejectsOptionsAndShellCharacters(string name) => Assert.False(DistroName.IsValid(name));
}
