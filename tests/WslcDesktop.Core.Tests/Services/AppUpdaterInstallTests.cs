using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class AppUpdaterInstallTests
{
    private const string Hash = "ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12";
    private const long Megabyte = 1024 * 1024;

    private readonly FakeInstalledApp _app = new() { DownloadedSize = 1000, DownloadedSha256 = Hash };

    private readonly FakeReleaseFeed _feed = new()
    {
        Latest = Release("1.2.0"),
        Checksums = $"{Hash}  WslcDesktop-win-x64.exe\n"
    };

    private readonly Notifier _notifier = new();
    private readonly InMemorySettings _settings = new();

    [Fact]
    public async Task InstallAsync_DownloadsVerifiesInstallsAndAsksForARestart()
    {
        AppUpdater updater = await CreateWithOfferAsync();
        int restarts = 0;
        updater.RestartRequested += () => restarts++;

        await updater.InstallAsync();

        Assert.Equal([FakeInstalledApp.DownloadPath], _feed.Downloads);
        Assert.Equal([FakeInstalledApp.DownloadPath], _app.Installed);
        Assert.Empty(_app.Discarded);
        Assert.Equal(new UpdateState(UpdateStatus.RestartRequired, Version("1.2.0")), updater.State);
        Assert.Equal(1, restarts);
        Assert.Null(_settings.Current.AvailableUpdate);
    }

    [Fact]
    public async Task InstallAsync_ReportsProgress()
    {
        _feed.Progress = [0.4];
        AppUpdater updater = await CreateWithOfferAsync();
        List<UpdateState> states = [];
        updater.Changed += () => states.Add(updater.State);

        await updater.InstallAsync();

        Assert.Contains(states, state => state is { Status: UpdateStatus.Downloading, Progress: 0.4 } && state.Version == Version("1.2.0"));
    }

    [Fact]
    public async Task InstallAsync_RejectsAChecksumMismatch()
    {
        _app.DownloadedSha256 = new string('0', 64);
        AppUpdater updater = await CreateWithOfferAsync();

        await updater.InstallAsync();

        AssertRejectedAfterDownload(updater, "The download did not match its checksum.");
    }

    [Fact]
    public async Task InstallAsync_RejectsADownloadOfTheWrongSize()
    {
        _app.DownloadedSize = 999;
        AppUpdater updater = await CreateWithOfferAsync();

        await updater.InstallAsync();

        AssertRejectedAfterDownload(updater, "The download did not match its checksum.");
    }

    [Fact]
    public async Task InstallAsync_DoesNotDownloadAReleaseOverTheSizeLimit()
    {
        _settings.Current.AvailableUpdate = "1.2.0";
        _feed.Latest = Release("1.2.0") with { AssetSize = 201 * Megabyte };
        AppUpdater updater = Create();

        await updater.InstallAsync();

        AssertNotInstallable(updater);
    }

    [Fact]
    public async Task InstallAsync_DiscardsADownloadThatWasCutOff()
    {
        _feed.DownloadFailure = new UpdateException("The download was interrupted.");
        AppUpdater updater = await CreateWithOfferAsync();

        await updater.InstallAsync();

        AssertRejectedAfterDownload(updater, "The download was interrupted.");
    }

    [Fact]
    public async Task InstallAsync_DoesNotDownload_WithoutAChecksumFile()
    {
        _settings.Current.AvailableUpdate = "1.2.0";
        _feed.Latest = Release("1.2.0") with { Checksums = null };
        AppUpdater updater = Create();

        await updater.InstallAsync();

        AssertNotInstallable(updater);
    }

    [Fact]
    public async Task InstallAsync_DoesNotDownload_WithoutALineForTheAsset()
    {
        _feed.Checksums = $"{Hash}  WslcDesktop-win-arm64.exe\n";
        AppUpdater updater = await CreateWithOfferAsync();

        await updater.InstallAsync();

        AssertRefusedBeforeDownload(updater, "The release has no checksum for this download.");
    }

    [Fact]
    public async Task InstallAsync_DoesNotDownload_WithoutAnAssetForThisArchitecture()
    {
        _settings.Current.AvailableUpdate = "1.2.0";
        _feed.Latest = Release("1.2.0") with { Asset = null };
        AppUpdater updater = Create();

        await updater.InstallAsync();

        AssertNotInstallable(updater);
    }

    [Fact]
    public async Task InstallAsync_DoesNotDownload_WhenTheLauncherFolderIsNotWritable()
    {
        _app.ReserveFailure = new UpdateException(@"WSLC Desktop cannot write to C:\Apps.");
        AppUpdater updater = await CreateWithOfferAsync();

        await updater.InstallAsync();

        AssertRefusedBeforeDownload(updater, @"WSLC Desktop cannot write to C:\Apps.");
    }

    [Fact]
    public async Task InstallAsync_Installs_WhenTheLauncherVersionCannotBeRead()
    {
        AppUpdater updater = await CreateWithOfferAsync();
        _app.LauncherVersion = null;

        await updater.InstallAsync();

        Assert.Single(_app.Installed);
        Assert.Equal(UpdateStatus.RestartRequired, updater.State.Status);
    }

    [Fact]
    public async Task InstallAsync_KeepsTheVersionOnOffer_WhenTheSwapFails()
    {
        _app.InstallFailure = new UpdateException("WSLC Desktop could not be replaced.");
        AppUpdater updater = await CreateWithOfferAsync();

        await updater.InstallAsync();

        AssertRejectedAfterDownload(updater, "WSLC Desktop could not be replaced.");
        Assert.Equal("1.2.0", _settings.Current.AvailableUpdate);
    }

    [Fact]
    public async Task InstallAsync_CanBeTriedAgain_AfterAFailure()
    {
        _app.InstallFailure = new UpdateException("WSLC Desktop could not be replaced.");
        AppUpdater updater = await CreateWithOfferAsync();
        await updater.InstallAsync();
        _app.InstallFailure = null;

        await updater.InstallAsync();

        Assert.Equal(UpdateStatus.RestartRequired, updater.State.Status);
    }

    [Fact]
    public async Task InstallAsync_IgnoresASecondRequestWhileOneRuns()
    {
        _feed.PendingDownload = new TaskCompletionSource();
        AppUpdater updater = await CreateWithOfferAsync();

        Task first = updater.InstallAsync();
        await updater.InstallAsync();
        await updater.CheckAsync(true, TestContext.Current.CancellationToken);
        Assert.Equal(UpdateStatus.Downloading, updater.State.Status);

        _feed.PendingDownload.SetResult();
        await first;

        Assert.Single(_feed.Downloads);
        Assert.Single(_app.Installed);
    }

    [Fact]
    public async Task InstallAsync_FetchesTheRelease_ForARememberedVersion()
    {
        _settings.Current.AvailableUpdate = "1.2.0";
        AppUpdater updater = Create();

        await updater.InstallAsync();

        Assert.Single(_feed.Requests);
        Assert.Single(_app.Installed);
    }

    [Fact]
    public async Task InstallAsync_NeverInstallsAnOlderVersion()
    {
        _settings.Current.AvailableUpdate = "1.2.0";
        _feed.Latest = Release("1.0.0");
        AppUpdater updater = Create();

        await updater.InstallAsync();

        Assert.Equal(UpdateStatus.UpToDate, updater.State.Status);
        Assert.Empty(_feed.Downloads);
        Assert.Empty(_app.Installed);
    }

    [Fact]
    public async Task InstallAsync_DoesNothing_WhenNoUpdateIsOnOffer()
    {
        AppUpdater updater = Create();

        await updater.InstallAsync();

        Assert.False(updater.CanInstall);
        Assert.Equal(UpdateStatus.NotChecked, updater.State.Status);
        Assert.Empty(_feed.Requests);
    }

    [Fact]
    public async Task CancelInstall_ReturnsToAvailableAndDiscardsTheDownload()
    {
        _feed.PendingDownload = new TaskCompletionSource();
        AppUpdater updater = await CreateWithOfferAsync();
        Task install = updater.InstallAsync();

        updater.CancelInstall();
        await install;

        Assert.Equal(UpdateStatus.Available, updater.State.Status);
        Assert.True(updater.CanInstall);
        Assert.Equal([FakeInstalledApp.DownloadPath], _app.Discarded);
        Assert.Empty(_app.Installed);
    }

    [Fact]
    public async Task CancelInstall_AfterTheDownload_DoesNotInstall()
    {
        _feed.Progress = [1];
        AppUpdater updater = await CreateWithOfferAsync();
        updater.Changed += () =>
        {
            if (updater.State is { Status: UpdateStatus.Downloading, Progress: 1 })
            {
                updater.CancelInstall();
            }
        };

        await updater.InstallAsync();

        Assert.Equal(UpdateStatus.Available, updater.State.Status);
        Assert.Empty(_app.Installed);
        Assert.Equal([FakeInstalledApp.DownloadPath], _app.Discarded);
    }

    [Fact]
    public async Task CancelInstall_DuringTheSwap_InstallsWithoutRestarting()
    {
        AppUpdater updater = await CreateWithOfferAsync();
        _app.OnInstall = updater.CancelInstall;
        int restarts = 0;
        updater.RestartRequested += () => restarts++;

        await updater.InstallAsync();

        Assert.Equal(UpdateStatus.RestartRequired, updater.State.Status);
        Assert.Equal(0, restarts);
    }

    [Fact]
    public async Task RequestRestart_RaisesTheEvent_OnlyWhenARestartIsPending()
    {
        AppUpdater updater = await CreateWithOfferAsync();
        int restarts = 0;
        updater.RestartRequested += () => restarts++;

        updater.RequestRestart();
        Assert.Equal(0, restarts);

        await updater.InstallAsync();
        updater.RequestRestart();
        Assert.Equal(2, restarts);
    }

    [Fact]
    public async Task UpdateAsync_InstallsWithoutANotification()
    {
        AppUpdater updater = await CreateWithOfferAsync();
        List<Notification> notifications = [];
        _notifier.Notified += notifications.Add;

        await updater.UpdateAsync();

        Assert.Single(_app.Installed);
        Assert.Empty(notifications);
    }

    [Fact]
    public async Task UpdateAsync_NotifiesOfTheReason_WhenTheInstallationFails()
    {
        _app.InstallFailure = new UpdateException("WSLC Desktop could not be replaced.");
        AppUpdater updater = await CreateWithOfferAsync();
        List<Notification> notifications = [];
        _notifier.Notified += notifications.Add;

        await updater.UpdateAsync();

        Notification notification = Assert.Single(notifications);
        Assert.Equal(("Could not update WSLC Desktop", "WSLC Desktop could not be replaced.", NotificationSeverity.Error),
            (notification.Title, notification.Message, notification.Severity));
    }

    [Fact]
    public async Task UpdateAsync_AsksForTheRestart_WhenTheUpdateIsAlreadyInstalled()
    {
        AppUpdater updater = await CreateWithOfferAsync();
        await updater.InstallAsync();
        int restarts = 0;
        updater.RestartRequested += () => restarts++;

        await updater.UpdateAsync();

        Assert.Equal(1, restarts);
        Assert.Single(_app.Installed);
    }

    [Fact]
    public async Task UpdateAsync_DoesNothing_WhenAnotherUpdateIsRunning()
    {
        _feed.PendingDownload = new TaskCompletionSource();
        AppUpdater updater = await CreateWithOfferAsync();
        Task first = updater.InstallAsync();

        await updater.UpdateAsync();

        Assert.Single(_feed.Downloads);

        _feed.PendingDownload.SetResult();
        await first;
    }

    [Theory]
    [InlineData("1.2.0")]
    [InlineData("1.2.5")]
    public async Task InstallAsync_AsksForARestart_WhenTheLauncherAlreadyHoldsTheVersion(string installedByTheOtherSide)
    {
        AppUpdater updater = await CreateWithOfferAsync();
        _app.LauncherVersion = Version(installedByTheOtherSide);
        int restarts = 0;
        updater.RestartRequested += () => restarts++;

        await updater.InstallAsync();

        Assert.Equal(new UpdateState(UpdateStatus.RestartRequired, Version(installedByTheOtherSide)), updater.State);
        Assert.Equal(1, restarts);
        Assert.Empty(_feed.Downloads);
        Assert.Empty(_app.Installed);
    }

    [Fact]
    public async Task InstallAsync_ReportsAnUnexpectedFailure()
    {
        _app.InstallFailure = new IOException("The disk is full.");
        AppUpdater updater = await CreateWithOfferAsync();

        await updater.InstallAsync();

        AssertRejectedAfterDownload(updater, "The disk is full.");
    }

    [Fact]
    public async Task InstallAsync_ShowsTheDownloadAtOnce()
    {
        AppUpdater updater = await CreateWithOfferAsync();
        List<UpdateState> states = [];
        updater.Changed += () => states.Add(updater.State);

        await updater.InstallAsync();

        Assert.Equal(new UpdateState(UpdateStatus.Downloading, Version("1.2.0"), Page: Release("1.2.0").Page), states[0]);
    }

    [Fact]
    public async Task InstallAsync_ReportsWholePercentsOnly()
    {
        _feed.Progress = [0.401, 0.404, 0.41, 0.41, 1];
        AppUpdater updater = await CreateWithOfferAsync();
        List<double> progress = [];
        updater.Changed += () =>
        {
            if (updater.State.Status == UpdateStatus.Downloading)
            {
                progress.Add(updater.State.Progress);
            }
        };

        await updater.InstallAsync();

        Assert.Equal([0, 0.4, 0.41, 1], progress);
    }

    private void AssertNotInstallable(AppUpdater updater)
    {
        Assert.Equal(
            new UpdateState(UpdateStatus.Failed, Message: "Version 1.2.0 is available, but cannot be installed from the app.", Page: Release("1.2.0").Page),
            updater.State);
        Assert.False(updater.CanInstall);
        Assert.Empty(_feed.Downloads);
        Assert.Null(_settings.Current.AvailableUpdate);
    }

    private void AssertRejectedAfterDownload(AppUpdater updater, string message)
    {
        AssertFailed(updater, message);
        Assert.Single(_feed.Downloads);
        Assert.Equal([FakeInstalledApp.DownloadPath], _app.Discarded);
    }

    private void AssertRefusedBeforeDownload(AppUpdater updater, string message)
    {
        AssertFailed(updater, message);
        Assert.Empty(_feed.Downloads);
    }

    private void AssertFailed(AppUpdater updater, string message)
    {
        Assert.Equal(new UpdateState(UpdateStatus.Failed, Version("1.2.0"), Message: message, Page: Release("1.2.0").Page), updater.State);
        Assert.True(updater.CanInstall);
        Assert.Empty(_app.Installed);
    }

    private AppUpdater Create() => new(_feed, _app, _settings, _notifier, new FixedTime());

    private async Task<AppUpdater> CreateWithOfferAsync()
    {
        AppUpdater updater = Create();
        await updater.CheckAsync(true, TestContext.Current.CancellationToken);
        Assert.Equal(UpdateStatus.Available, updater.State.Status);
        return updater;
    }

    private static AppVersion Version(string text) => FakeInstalledApp.Version(text);

    private static AppRelease Release(string version) => new(
        Version(version),
        new Uri($"https://github.com/yzhoholiev/wslc-desktop/releases/tag/v{version}"),
        new Uri($"https://github.com/yzhoholiev/wslc-desktop/releases/download/v{version}/WslcDesktop-win-x64.exe"),
        1000,
        new Uri($"https://github.com/yzhoholiev/wslc-desktop/releases/download/v{version}/SHA256SUMS.txt"));
}
