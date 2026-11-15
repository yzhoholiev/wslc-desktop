using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class ChecksumFileTests
{
    private const string X64 = "WslcDesktop-win-x64.exe";
    private const string X64Hash = "ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12";
    private const string Arm64Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly string Sums = $"{X64Hash.ToUpperInvariant()}  {X64}\n{Arm64Hash}  WslcDesktop-win-arm64.exe\n";

    [Fact]
    public void Find_ReturnsTheLowercaseHashOfTheAsset()
    {
        Assert.Equal(X64Hash, ChecksumFile.Find(Sums, X64));
        Assert.Equal(Arm64Hash, ChecksumFile.Find(Sums, "WslcDesktop-win-arm64.exe"));
    }

    [Fact]
    public void Find_AcceptsCrLfAndTheBinaryMarker() => Assert.Equal(X64Hash, ChecksumFile.Find($"{X64Hash} *{X64}\r\n", X64));

    [Fact]
    public void Find_ReturnsNullForAMissingAsset() => Assert.Null(ChecksumFile.Find(Sums, "WslcDesktop-win-x86.exe"));

    [Theory]
    [InlineData("ab12  WslcDesktop-win-x64.exe")]
    [InlineData("zz12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12  WslcDesktop-win-x64.exe")]
    [InlineData("WslcDesktop-win-x64.exe")]
    [InlineData("")]
    public void Find_ReturnsNullForALineWithoutASha256(string text) => Assert.Null(ChecksumFile.Find(text, X64));

    [Fact]
    public void Find_DoesNotMatchANameThatOnlyEndsWithTheAsset() => Assert.Null(ChecksumFile.Find($"{X64Hash}  evil-{X64}\n", X64));
}
