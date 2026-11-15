using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class InputValidationTests
{
    [Fact]
    public void SessionRunArguments_NameTheSessionSoWslcCannotCreateIt() =>
        Assert.Equal(["--session", "s", "system", "session", "run", "df", "-B1"], WslcCli.SessionRunArguments("s", "df", "-B1"));

    [Fact]
    public void Launcher_AcceptsAFullPathToAnExistingFile()
    {
        string file = Path.Combine(Path.GetTempPath(), $"wslc-launcher-{Guid.NewGuid():N}.exe");
        File.WriteAllText(file, "");
        try
        {
            Assert.Equal(file, Launcher.Accept(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"WslcDesktop.exe")]
    [InlineData(@"C:\does\not\exist\WslcDesktop.exe")]
    [InlineData("C:\\Windows\\System32\\notepad.exe\" --evil \"")]
    public void Launcher_RejectsRelativeMissingOrQuotedPaths(string? path) => Assert.Null(Launcher.Accept(path));

    [Theory]
    [InlineData("https://github.com/yzhoholiev/wslc-desktop/releases", true)]
    [InlineData("https://api.github.com/repos/yzhoholiev/wslc-desktop/releases/latest", true)]
    [InlineData("https://release-assets.githubusercontent.com/x", true)]
    [InlineData("https://objects.githubusercontent.com/x", true)]
    [InlineData("HTTPS://GITHUB.COM/a/b", true)]
    [InlineData("http://github.com/a/b", false)]
    [InlineData("https://github.com.evil.example/x", false)]
    [InlineData("https://evilgithub.com/x", false)]
    [InlineData("https://githubusercontent.com.evil.example/x", false)]
    [InlineData("https://evilgithubusercontent.com/x", false)]
    [InlineData("https://user:pw@github.com/x", false)]
    [InlineData("https://github.com:8443/x", false)]
    [InlineData("file:///C:/x.exe", false)]
    [InlineData("/relative/path", false)]
    public void GitHubUrl_AllowsOnlyHttpsOnGitHubHosts(string url, bool allowed) =>
        Assert.Equal(allowed, GitHubUrl.IsAllowed(new Uri(url, UriKind.RelativeOrAbsolute)));
}
