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
}
