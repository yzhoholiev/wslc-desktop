using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    private readonly TestServices _test = new();

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
