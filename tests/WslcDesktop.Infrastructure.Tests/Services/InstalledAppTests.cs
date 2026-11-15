using System.Diagnostics;
using System.Runtime.InteropServices;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class InstalledAppTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "WslcDesktop.Tests", $"installed-{Guid.NewGuid():N}");
    private readonly string _launcher;

    public InstalledAppTests()
    {
        Directory.CreateDirectory(_folder);
        _launcher = Path.Combine(_folder, "WslcDesktop.exe");
        File.WriteAllText(_launcher, "old");
    }

    public void Dispose() => Directory.Delete(_folder, true);

    [Fact]
    public void ReadLauncherVersion_ReturnsNull_WhenTheFileIsGone()
    {
        File.Delete(_launcher);

        Assert.Null(App().ReadLauncherVersion());
    }

    [Fact]
    public void ReadLauncherVersion_ReturnsNull_ForAFileWithoutAVersion() => Assert.Null(App().ReadLauncherVersion());

    [Fact]
    public void ReadLauncherVersion_ReadsTheProductVersion()
    {
        string host = Environment.ProcessPath!;
        Assert.True(AppVersion.TryParse(FileVersionInfo.GetVersionInfo(host).ProductVersion, out AppVersion? expected));

        Assert.Equal(expected, App(host).ReadLauncherVersion());
    }

    [Fact]
    public void ReserveDownloadPath_IsBesideTheLauncher()
    {
        string path = App().ReserveDownloadPath();

        Assert.Equal(_folder, Path.GetDirectoryName(path));
        Assert.StartsWith("WslcDesktop.exe.", Path.GetFileName(path));
        Assert.EndsWith(".download", path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void ReserveDownloadPath_Throws_WhenTheLauncherIsGone()
    {
        File.Delete(_launcher);

        UpdateException failure = Assert.Throws<UpdateException>(App().ReserveDownloadPath);

        Assert.Equal("WSLC Desktop was moved or deleted since it started.", failure.Message);
    }

    [Fact]
    public void ReserveDownloadPath_RemovesLeftoversOlderThanADay()
    {
        string stale = $"{_launcher}.abc.download";
        string staleBackup = $"{_launcher}.old";
        string fresh = $"{_launcher}.def.download";
        string unrelated = Path.Combine(_folder, "notes.download");
        foreach (string file in new[] { stale, staleBackup, fresh, unrelated })
        {
            File.WriteAllText(file, "");
        }

        DateTime twoDaysAgo = DateTime.UtcNow.AddDays(-2);
        File.SetLastWriteTimeUtc(stale, twoDaysAgo);
        File.SetLastWriteTimeUtc(staleBackup, twoDaysAgo);
        File.SetLastWriteTimeUtc(unrelated, twoDaysAgo);

        App().ReserveDownloadPath();

        Assert.False(File.Exists(stale));
        Assert.False(File.Exists(staleBackup));
        Assert.True(File.Exists(fresh));
        Assert.True(File.Exists(unrelated));
    }

    [Fact]
    public async Task InspectAsync_ReturnsSizeAndLowercaseSha256()
    {
        string file = Path.Combine(_folder, "abc.bin");
        await File.WriteAllTextAsync(file, "abc", TestContext.Current.CancellationToken);

        (long size, string sha256) = await App().InspectAsync(file, TestContext.Current.CancellationToken);

        Assert.Equal(3, size);
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", sha256);
    }

    [Fact]
    public void Install_ReplacesTheLauncherAndLeavesNoBackup()
    {
        string download = Download("new");

        App().Install(download);

        Assert.Equal("new", File.ReadAllText(_launcher));
        Assert.Equal(["WslcDesktop.exe"], Directory.GetFiles(_folder).Select(Path.GetFileName));
    }

    [Fact]
    public void Install_KeepsTheLauncher_WhenTheDownloadIsMissing()
    {
        Assert.Throws<UpdateException>(() => App().Install(Path.Combine(_folder, "missing.download")));

        Assert.Equal("old", File.ReadAllText(_launcher));
    }

    [Fact]
    public void Install_KeepsTheLauncher_WhenItStaysLocked()
    {
        string download = Download("new");
        using (new FileStream(_launcher, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Throws<UpdateException>(() => App().Install(download));
        }

        Assert.Equal("old", File.ReadAllText(_launcher));
    }

    [Fact]
    public async Task Install_Retries_WhileTheLauncherIsBrieflyLocked()
    {
        string download = Download("new");
        var locked = new FileStream(_launcher, FileMode.Open, FileAccess.Read, FileShare.Read);
        Task release = Task.Delay(400, TestContext.Current.CancellationToken).ContinueWith(_ => locked.Dispose(), TaskScheduler.Default);

        App().Install(download);
        await release;

        Assert.Equal("new", File.ReadAllText(_launcher));
    }

    [Fact]
    public void Discard_DeletesTheFileAndToleratesAMissingOne()
    {
        string download = Download("new");

        App().Discard(download);
        App().Discard(download);

        Assert.False(File.Exists(download));
    }

    [Theory]
    [InlineData(Architecture.X64, "WslcDesktop-win-x64.exe")]
    [InlineData(Architecture.Arm64, "WslcDesktop-win-arm64.exe")]
    public void AssetName_FollowsTheArchitecture(Architecture architecture, string expected) =>
        Assert.Equal(expected, new InstalledApp(_launcher, Version("1.1.0"), architecture).AssetName);

    [Fact]
    public void HasLauncher_IsFalse_ForABuildOutput()
    {
        var app = new InstalledApp(null, Version("1.1.0"), Architecture.X64);

        Assert.False(app.HasLauncher);
        Assert.Null(app.ReadLauncherVersion());
        Assert.Throws<UpdateException>(app.ReserveDownloadPath);
    }

    [Fact]
    public void ReadRunningVersion_ReadsThisProcess() => Assert.NotNull(InstalledApp.ReadRunningVersion());

    private InstalledApp App(string? launcher = null) => new(launcher ?? _launcher, Version("1.1.0"), Architecture.X64);

    private string Download(string content)
    {
        string path = $"{_launcher}.test.download";
        File.WriteAllText(path, content);
        return path;
    }

    private static AppVersion Version(string text) => AppVersion.TryParse(text, out AppVersion? version) ? version : throw new FormatException(text);
}
