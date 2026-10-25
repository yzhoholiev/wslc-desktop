namespace WslcDesktop.Services;

/// <summary>
///     Builds the <see cref="AppServices" /> the app runs on, over the real adapters.
/// </summary>
public static class DefaultServices
{
    public static AppServices Create()
    {
        var notifier = new Notifier();
        var wslc = new WslcCli();
        var reader = new FallbackRuntimeReader(
            () => new DockerEngine(new SessionShell(Wslc.DefaultSessionName)),
            new CliRuntimeReader(),
            TimeProvider.System);

        return new AppServices(
            reader,
            reader,
            wslc,
            wslc,
            new WslcSessionHosts(),
            new WslCli(),
            new WslConfigFile(),
            new WslcSettingsFile(),
            new ShellLauncher(notifier),
            new SettingsStore(SettingsStore.DefaultFilePath),
            notifier);
    }
}
