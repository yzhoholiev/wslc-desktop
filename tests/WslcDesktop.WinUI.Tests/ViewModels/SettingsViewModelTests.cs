using WslcDesktop.Models;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    private readonly TestServices _test = new();

    [Fact]
    public async Task UpdateStatusText_FollowsTheUpdater()
    {
        _test.Releases.Latest = Release("1.2.0");
        var viewModel = new SettingsViewModel(_test.Services);
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        viewModel.Attach();
        Assert.Equal("Not checked yet", viewModel.UpdateStatusText);
        Assert.False(viewModel.CanUpdate);

        await viewModel.CheckNowAsync();

        Assert.Equal("Version 1.2.0 is available", viewModel.UpdateStatusText);
        Assert.True(viewModel.CanUpdate);
        Assert.True(viewModel.CanCheck);
        Assert.False(viewModel.CanRestart);
        Assert.Contains(nameof(SettingsViewModel.UpdateStatusText), changed);
        Assert.Contains(nameof(SettingsViewModel.CanUpdate), changed);

        viewModel.Detach();
        changed.Clear();
        await viewModel.CheckNowAsync();
        Assert.Empty(changed);
    }

    [Fact]
    public async Task UpdateAsync_InstallsAndOffersTheRestart()
    {
        _test.Releases.Latest = Release("1.2.0");
        _test.Releases.Checksums = $"{Hash}  WslcDesktop-win-x64.exe";
        _test.Installed.DownloadedSize = 1000;
        _test.Installed.DownloadedSha256 = Hash;
        var viewModel = new SettingsViewModel(_test.Services);
        viewModel.Attach();
        await viewModel.CheckNowAsync();

        await viewModel.UpdateAsync();

        Assert.Single(_test.Installed.Installed);
        Assert.True(viewModel.CanRestart);
        Assert.False(viewModel.CanUpdate);
        Assert.Equal("Restart to finish updating to 1.2.0", viewModel.UpdateStatusText);
    }

    [Fact]
    public async Task Download_ShowsProgressAndCanBeCancelled()
    {
        _test.Releases.Latest = Release("1.2.0");
        _test.Releases.Checksums = $"{Hash}  WslcDesktop-win-x64.exe";
        _test.Releases.Progress = [0.4];
        _test.Releases.PendingDownload = new TaskCompletionSource();
        var viewModel = new SettingsViewModel(_test.Services);
        viewModel.Attach();
        await viewModel.CheckNowAsync();

        Task update = viewModel.UpdateAsync();

        Assert.True(viewModel.IsDownloading);
        Assert.True(viewModel.CanCancelUpdate);
        Assert.False(viewModel.CanCheck);
        Assert.False(viewModel.CanUpdate);
        Assert.Equal(40, viewModel.DownloadProgress);

        viewModel.CancelUpdate();
        await update;

        Assert.False(viewModel.IsDownloading);
        Assert.True(viewModel.CanUpdate);
    }

    [Fact]
    public void UpdateSection_IsDisabled_WithoutALauncher()
    {
        var viewModel = new SettingsViewModel(new TestServices(false).Services);

        Assert.False(viewModel.IsUpdateSectionEnabled);
        Assert.False(viewModel.CanCheck);
        Assert.Equal("Updates are available only in the released app.", viewModel.UpdateStatusText);
    }

    [Fact]
    public void CheckForUpdates_IsOnByDefaultAndSaved()
    {
        var viewModel = new SettingsViewModel(_test.Services);
        Assert.True(viewModel.CheckForUpdates);

        viewModel.CheckForUpdates = false;

        Assert.False(_test.Settings.Current.CheckForUpdates);
        Assert.Equal(1, _test.Settings.Saves);
    }

    [Fact]
    public async Task OpenReleasePage_OpensTheReleaseOfTheOfferedVersion()
    {
        var viewModel = new SettingsViewModel(_test.Services);
        viewModel.OpenReleasePage();
        _test.Releases.Latest = Release("1.2.0");
        await viewModel.CheckNowAsync();

        viewModel.OpenReleasePage();

        Assert.Equal(
            ["url https://github.com/yzhoholiev/wslc-desktop/releases", "url https://github.com/yzhoholiev/wslc-desktop/releases/tag/v1.2.0"],
            _test.Shell.Opened);
    }

    private const string Hash = "ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12";

    private static AppRelease Release(string version) => new(
        FakeInstalledApp.Version(version),
        new Uri($"https://github.com/yzhoholiev/wslc-desktop/releases/tag/v{version}"),
        new Uri($"https://github.com/yzhoholiev/wslc-desktop/releases/download/v{version}/WslcDesktop-win-x64.exe"),
        1000,
        new Uri($"https://github.com/yzhoholiev/wslc-desktop/releases/download/v{version}/SHA256SUMS.txt"));

    [Fact]
    public void Toggles_SaveOnlyWhatChanged()
    {
        var viewModel = new SettingsViewModel(_test.Services) { KeepRunningInTray = true };
        Assert.Equal(0, _test.Settings.Saves);

        viewModel.KeepRunningInTray = false;
        viewModel.StartRuntimeOnLaunch = true;
        viewModel.StopRuntimeOnExit = true;

        Assert.False(_test.Settings.Current.KeepRunningInTray);
        Assert.True(_test.Settings.Current.StartRuntimeOnLaunch);
        Assert.True(_test.Settings.Current.StopRuntimeOnExit);
        Assert.Equal(3, _test.Settings.Saves);
    }

    [Fact]
    public void SetStartWithWindows_ReturnsTheNewState()
    {
        var viewModel = new SettingsViewModel(_test.Services);

        Assert.True(viewModel.SetStartWithWindows(true));
        Assert.True(_test.Settings.IsStartWithWindowsEnabled);
        Assert.False(viewModel.SetStartWithWindows(false));
    }

    [Fact]
    public void SetStartWithWindows_ReportsARefusalAndKeepsTheOldState()
    {
        _test.Settings.StartWithWindowsFailure = new UnauthorizedAccessException("Access denied.");
        List<Notification> notifications = [];
        _test.Notifier.Notified += notifications.Add;

        bool enabled = new SettingsViewModel(_test.Services).SetStartWithWindows(true);

        Assert.False(enabled);
        Assert.Equal([new Notification(NotificationSeverity.Error, "Could not change the startup setting", "Access denied.")], notifications);
    }

    [Fact]
    public void ChangeBackdrop_SavesANewChoiceOnly()
    {
        var viewModel = new SettingsViewModel(_test.Services);

        Assert.False(viewModel.ChangeBackdrop(viewModel.Backdrop));
        Assert.True(viewModel.ChangeBackdrop("MicaAlt"));

        Assert.Equal("MicaAlt", _test.Settings.Current.Backdrop);
        Assert.Equal(1, _test.Settings.Saves);
    }

    [Fact]
    public void Texts_NameTheSessionTheVersionAndWslc()
    {
        var viewModel = new SettingsViewModel(_test.Services);

        Assert.Contains(Wslc.DefaultSessionName, viewModel.StartRuntimeOnLaunchDescription);
        Assert.Contains(Wslc.DefaultSessionName, viewModel.StopRuntimeOnExitDescription);
        Assert.StartsWith("WSLC Desktop ", viewModel.VersionText);
        Assert.DoesNotContain('+', viewModel.VersionText);
        Assert.Equal($"wslc: {_test.Details.ExecutablePath}", viewModel.WslcPathText);
    }

    [Fact]
    public void OpenDataFolderAndLicenses_OpenWhatTheyName()
    {
        var viewModel = new SettingsViewModel(_test.Services);

        viewModel.OpenDataFolder();
        viewModel.OpenLicenses();

        Assert.Equal(["data-folder", "licenses"], _test.Shell.Opened);
    }
}
