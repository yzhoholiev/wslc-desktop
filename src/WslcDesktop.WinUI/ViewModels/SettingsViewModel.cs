using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;
using System.Security;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

/// <summary>
///     The settings page: the stored preferences, the start-with-Windows entry, the app's updates and the version shown in About.
/// </summary>
public sealed class SettingsViewModel(AppServices services) : ObservableObject
{
    private static readonly string[] UpdateProperties =
    [
        nameof(UpdateStatusText), nameof(CanCheck), nameof(CanUpdate), nameof(CanCancelUpdate), nameof(CanRestart), nameof(IsDownloading),
        nameof(DownloadProgress)
    ];

    private readonly ISettingsStore _settings = services.Settings;
    private readonly AppUpdater _updater = services.Updater;

    public bool CheckForUpdates
    {
        get => _settings.Current.CheckForUpdates;
        set => Save(_settings.Current.CheckForUpdates, value, () => _settings.Current.CheckForUpdates = value);
    }

    public string UpdateStatusText => Labels.UpdateStatus(_updater.State);

    public bool IsUpdateSectionEnabled => _updater.State.Status != UpdateStatus.Unavailable;

    public bool CanCheck => _updater.State.Status is not (UpdateStatus.Unavailable or UpdateStatus.Checking or UpdateStatus.Downloading);

    public bool CanUpdate => _updater.CanInstall;

    public bool CanCancelUpdate => IsDownloading;

    public bool CanRestart => _updater.State.Status == UpdateStatus.RestartRequired;

    public bool IsDownloading => _updater.State.Status == UpdateStatus.Downloading;

    /// <summary>
    ///     The downloaded share of the update, from 0 to 100.
    /// </summary>
    public double DownloadProgress => Math.Clamp(_updater.State.Progress, 0, 1) * 100;

    public bool KeepRunningInTray
    {
        get => _settings.Current.KeepRunningInTray;
        set => Save(_settings.Current.KeepRunningInTray, value, () => _settings.Current.KeepRunningInTray = value);
    }

    public bool StartRuntimeOnLaunch
    {
        get => _settings.Current.StartRuntimeOnLaunch;
        set => Save(_settings.Current.StartRuntimeOnLaunch, value, () => _settings.Current.StartRuntimeOnLaunch = value);
    }

    public bool StopRuntimeOnExit
    {
        get => _settings.Current.StopRuntimeOnExit;
        set => Save(_settings.Current.StopRuntimeOnExit, value, () => _settings.Current.StopRuntimeOnExit = value);
    }

    public bool IsStartWithWindowsEnabled => _settings.IsStartWithWindowsEnabled;

    public string Backdrop => _settings.Current.Backdrop;

    public string StartRuntimeOnLaunchDescription { get; } =
        $"Starts session {Wslc.DefaultSessionName} whenever WSLC Desktop starts, including at sign-in; stopped containers stay stopped.";

    public string StopRuntimeOnExitDescription { get; } =
        $"Shuts down session {Wslc.DefaultSessionName} and releases its VM; running containers stop but are kept. "
        + "Closing the window counts as exiting only when the app does not keep running in the notification area.";

    public string VersionText { get; } =
        $"WSLC Desktop {(typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown").Split('+')[0]}";

    public string WslcPathText { get; } = $"wslc: {services.Details.ExecutablePath}";

    /// <summary>
    ///     Turns starting with Windows on or off, and returns whether it is on afterwards, which differs when Windows refused the change.
    /// </summary>
    public bool SetStartWithWindows(bool enabled)
    {
        if (enabled == _settings.IsStartWithWindowsEnabled)
        {
            return enabled;
        }

        try
        {
            _settings.IsStartWithWindowsEnabled = enabled;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            services.Notifier.Error("Could not change the startup setting", ex.Message);
        }

        return _settings.IsStartWithWindowsEnabled;
    }

    /// <summary>
    ///     Stores the window background, and returns whether it changed and must be applied.
    /// </summary>
    public bool ChangeBackdrop(string backdrop)
    {
        if (backdrop == _settings.Current.Backdrop)
        {
            return false;
        }

        _settings.Current.Backdrop = backdrop;
        _settings.Save();
        return true;
    }

    /// <summary>
    ///     Follows the updater while the page is shown.
    /// </summary>
    public void Attach()
    {
        _updater.Changed -= OnUpdaterChanged;
        _updater.Changed += OnUpdaterChanged;
        OnUpdaterChanged();
    }

    public void Detach() => _updater.Changed -= OnUpdaterChanged;

    public Task CheckNowAsync() => _updater.CheckAsync(true);

    public Task UpdateAsync() => _updater.InstallAsync();

    public void CancelUpdate() => _updater.CancelInstall();

    public void Restart() => _updater.RequestRestart();

    /// <summary>
    ///     Opens the notes of the version on offer, or the list of releases when there is none.
    /// </summary>
    public void OpenReleasePage() => services.Shell.OpenUrl(_updater.State.Page ?? AppUpdater.ReleasesPage);

    public void OpenDataFolder() => services.Shell.OpenDataFolder();

    public void OpenLicenses() => services.Shell.OpenLicenses();

    private void OnUpdaterChanged()
    {
        foreach (string property in UpdateProperties)
        {
            OnPropertyChanged(property);
        }
    }

    private void Save(bool current, bool value, Action set)
    {
        if (current == value)
        {
            return;
        }

        set();
        _settings.Save();
    }
}
