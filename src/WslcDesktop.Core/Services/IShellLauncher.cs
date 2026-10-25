namespace WslcDesktop.Services;

/// <summary>
///     Opens terminals, editors, folders and links outside the app, reporting failures instead of throwing.
/// </summary>
public interface IShellLauncher
{
    void OpenContainerShell(string containerId);

    void OpenDistroShell(string distro);

    void OpenSessionShell(string session);

    void OpenWslConfig();

    void OpenWslSettings();

    void OpenUrl(Uri url);

    /// <summary>
    ///     Opens the folder with the settings file and the crash log.
    /// </summary>
    void OpenDataFolder();

    /// <summary>
    ///     Opens the app's license and the notices of the components it ships with.
    /// </summary>
    void OpenLicenses();
}
