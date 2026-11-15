using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class AppUpdaterCheckTests
{
    private readonly FakeInstalledApp _app = new();
    private readonly FakeReleaseFeed _feed = new();
    private readonly InMemorySettings _settings = new();
    private readonly FixedTime _time = new();

    [Fact]
    public async Task State_IsUnavailable_WithoutALauncher()
    {
        _app.HasLauncher = false;
        AppUpdater updater = Create();

        await updater.CheckAsync(true, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.Unavailable, updater.State.Status);
        Assert.Empty(_feed.Requests);
    }

    [Fact]
    public void State_IsNotChecked_BeforeAnyCheck() => Assert.Equal(UpdateStatus.NotChecked, Create().State.Status);

    [Fact]
    public void State_RemembersTheVersionTheLastCheckFound()
    {
        _settings.Current.AvailableUpdate = "1.2.0";
        _settings.Current.LastUpdateCheck = _time.GetUtcNow();

        UpdateState state = Create().State;

        Assert.Equal(UpdateStatus.Available, state.Status);
        Assert.Equal(Version("1.2.0"), state.Version);
        Assert.Equal(new Uri("https://github.com/yzhoholiev/wslc-desktop/releases/tag/v1.2.0"), state.Page);
    }

    [Theory]
    [InlineData("1.2.0")]
    [InlineData("not a version")]
    public void State_IsUpToDate_WhenTheRememberedVersionIsNotNewer(string remembered)
    {
        _app.RunningVersion = Version("1.2.0");
        _app.LauncherVersion = Version("1.2.0");
        _settings.Current.AvailableUpdate = remembered;
        _settings.Current.LastUpdateCheck = _time.GetUtcNow();

        Assert.Equal(UpdateStatus.UpToDate, Create().State.Status);
    }

    [Fact]
    public void State_AsksForARestart_WhenTheLauncherIsAlreadyNewer()
    {
        _app.LauncherVersion = Version("1.2.0");

        UpdateState state = Create().State;

        Assert.Equal(UpdateStatus.RestartRequired, state.Status);
        Assert.Equal(Version("1.2.0"), state.Version);
    }

    [Fact]
    public async Task CheckAsync_OffersANewerRelease()
    {
        _feed.Latest = Release("1.2.0");
        AppUpdater updater = Create();
        List<UpdateStatus> changes = [];
        updater.Changed += () => changes.Add(updater.State.Status);

        await updater.CheckAsync(true, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.Available, updater.State.Status);
        Assert.Equal(Version("1.2.0"), updater.State.Version);
        Assert.Equal(_feed.Latest.Page, updater.State.Page);
        Assert.Equal([UpdateStatus.Checking, UpdateStatus.Available], changes);
        Assert.Equal("1.2.0", _settings.Current.AvailableUpdate);
        Assert.Equal(_time.GetUtcNow(), _settings.Current.LastUpdateCheck);
        Assert.Equal(1, _settings.Saves);
    }

    [Theory]
    [InlineData("1.1.0")]
    [InlineData("1.0.0")]
    [InlineData(null)]
    public async Task CheckAsync_IgnoresAReleaseThatIsNotNewer(string? latest)
    {
        _settings.Current.AvailableUpdate = "1.0.5";
        _feed.Latest = latest is null ? null : Release(latest);
        AppUpdater updater = Create();

        await updater.CheckAsync(true, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.UpToDate, updater.State.Status);
        Assert.Null(_settings.Current.AvailableUpdate);
        Assert.NotNull(_settings.Current.LastUpdateCheck);
    }

    [Theory]
    [InlineData("1.1.0", false)]
    [InlineData("1.1.1-10", true)]
    public async Task CheckAsync_AsksForPreReleasesOnlyFromAPreReleaseBuild(string running, bool expected)
    {
        Run(running);

        await Create().CheckAsync(true, TestContext.Current.CancellationToken);

        Assert.Equal([expected], _feed.Requests);
    }

    [Fact]
    public async Task CheckAsync_OffersThePreReleaseToAnUntaggedBuild()
    {
        Run("1.1.1-10");
        _feed.Latest = Release("1.1.1-rc.1");
        AppUpdater updater = Create();

        await updater.CheckAsync(false, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.Available, updater.State.Status);
    }

    [Fact]
    public async Task CheckAsync_ShowsTheReason_WhenAManualCheckFails()
    {
        _feed.Failure = new UpdateException("GitHub is not reachable.");
        AppUpdater updater = Create();

        await updater.CheckAsync(true, TestContext.Current.CancellationToken);

        Assert.Equal(new UpdateState(UpdateStatus.Failed, Message: "GitHub is not reachable."), updater.State);
        Assert.Null(_settings.Current.LastUpdateCheck);
    }

    [Fact]
    public async Task CheckAsync_KeepsTheVersionOnOffer_WhenAManualCheckFails()
    {
        _settings.Current.AvailableUpdate = "1.2.0";
        _feed.Failure = new UpdateException("GitHub is not reachable.");
        AppUpdater updater = Create();

        await updater.CheckAsync(true, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.Failed, updater.State.Status);
        Assert.Equal(Version("1.2.0"), updater.State.Version);
    }

    [Fact]
    public async Task CheckAsync_StaysQuiet_WhenAnAutomaticCheckFails()
    {
        _feed.Failure = new UpdateException("GitHub is not reachable.");
        AppUpdater updater = Create();

        await updater.CheckAsync(false, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.NotChecked, updater.State.Status);
        Assert.Null(_settings.Current.LastUpdateCheck);
    }

    [Theory]
    [InlineData("asset")]
    [InlineData("checksums")]
    [InlineData("size")]
    public async Task CheckAsync_DoesNotOfferAReleaseItCannotInstall(string missing)
    {
        AppRelease release = Release("1.2.0");
        _feed.Latest = missing switch
        {
            "asset" => release with { Asset = null },
            "checksums" => release with { Checksums = null },
            _ => release with { AssetSize = 201L * 1024 * 1024 }
        };
        AppUpdater updater = Create();

        await updater.CheckAsync(false, TestContext.Current.CancellationToken);

        Assert.Equal(
            new UpdateState(UpdateStatus.Failed, Message: "Version 1.2.0 is available, but cannot be installed from the app.", Page: release.Page),
            updater.State);
        Assert.False(updater.CanInstall);
        Assert.Null(updater.TakeAnnouncement());
        Assert.Null(_settings.Current.AvailableUpdate);
        Assert.NotNull(_settings.Current.LastUpdateCheck);
    }

    [Fact]
    public async Task CheckAsync_ShowsTheReason_WhenAManualCheckFailsUnexpectedly()
    {
        _feed.Failure = new InvalidOperationException("Something broke.");
        AppUpdater updater = Create();

        await updater.CheckAsync(true, TestContext.Current.CancellationToken);

        Assert.Equal(new UpdateState(UpdateStatus.Failed, Message: "Something broke."), updater.State);
    }

    [Fact]
    public async Task CheckAsync_StaysQuiet_WhenAnAutomaticCheckFailsUnexpectedly()
    {
        _feed.Failure = new InvalidOperationException("Something broke.");
        AppUpdater updater = Create();

        await updater.CheckAsync(false, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.NotChecked, updater.State.Status);
    }

    [Fact]
    public async Task CheckAsync_DetectsANewerLauncherWithoutTheNetwork()
    {
        AppUpdater updater = Create();
        _app.LauncherVersion = Version("1.2.0");

        await updater.CheckAsync(true, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.RestartRequired, updater.State.Status);
        Assert.Empty(_feed.Requests);
    }

    [Fact]
    public void Start_ChecksOnce()
    {
        Create().Start();

        Assert.Single(_feed.Requests);
    }

    [Fact]
    public void Start_Checks_EvenShortlyAfterTheLastCheck()
    {
        _settings.Current.LastUpdateCheck = _time.GetUtcNow() - TimeSpan.FromMinutes(5);

        Create().Start();

        Assert.Single(_feed.Requests);
    }

    [Fact]
    public void Start_DoesNotCheck_WhenAutomaticChecksAreOff()
    {
        _settings.Current.CheckForUpdates = false;

        Create().Start();

        Assert.Empty(_feed.Requests);
    }

    [Fact]
    public void Start_DoesNothing_WithoutALauncher()
    {
        _app.HasLauncher = false;

        Create().Start();

        Assert.Empty(_feed.Requests);
    }

    [Fact]
    public async Task CheckAsync_KeepsTheOffer_DuringAnAutomaticCheck()
    {
        _feed.Latest = Release("1.2.0");
        AppUpdater updater = Create();
        await updater.CheckAsync(false, TestContext.Current.CancellationToken);
        List<UpdateStatus> statuses = [];
        updater.Changed += () => statuses.Add(updater.State.Status);

        await updater.CheckAsync(false, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(UpdateStatus.Checking, statuses);
        Assert.True(updater.CanInstall);
    }

    [Fact]
    public async Task TakeAnnouncement_ReturnsAVersionOnce()
    {
        _feed.Latest = Release("1.2.0");
        AppUpdater updater = Create();
        Assert.Null(updater.TakeAnnouncement());

        await updater.CheckAsync(false, TestContext.Current.CancellationToken);

        Assert.Equal(Version("1.2.0"), updater.TakeAnnouncement());
        Assert.Null(updater.TakeAnnouncement());
        Assert.Equal("1.2.0", _settings.Current.AnnouncedUpdate);
        Assert.Null(Create().TakeAnnouncement());
    }

    [Fact]
    public async Task TakeAnnouncement_ReturnsNull_WhenTheOtherSideAnnouncedTheVersion()
    {
        _feed.Latest = Release("1.2.0");
        _settings.Saved = new AppSettings { AnnouncedUpdate = "1.2.0" };
        AppUpdater updater = Create();
        await updater.CheckAsync(false, TestContext.Current.CancellationToken);

        Assert.Null(updater.TakeAnnouncement());
        Assert.Equal(1, _settings.Saves);
    }

    private AppUpdater Create() => new(_feed, _app, _settings, new Notifier(), _time);

    private void Run(string version)
    {
        _app.RunningVersion = Version(version);
        _app.LauncherVersion = Version(version);
    }

    private static AppVersion Version(string text) => FakeInstalledApp.Version(text);

    private static AppRelease Release(string version) => new(
        Version(version),
        new Uri($"https://github.com/yzhoholiev/wslc-desktop/releases/tag/v{version}"),
        new Uri($"https://github.com/yzhoholiev/wslc-desktop/releases/download/v{version}/WslcDesktop-win-x64.exe"),
        1000,
        new Uri($"https://github.com/yzhoholiev/wslc-desktop/releases/download/v{version}/SHA256SUMS.txt"));
}
