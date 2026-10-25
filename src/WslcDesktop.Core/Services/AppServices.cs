namespace WslcDesktop.Services;

/// <summary>
///     The services one app process uses: the adapters it is given, and the use cases built over them.
/// </summary>
public sealed class AppServices
{
    public AppServices(
        IRuntimeReader reader,
        IRuntimeSession session,
        IRuntimeCommands commands,
        IRuntimeDetails details,
        ISessionHosts hosts,
        IWslPlatform wsl,
        IWslConfig wslConfig,
        IWslcSettings wslcSettings,
        IShellLauncher shell,
        ISettingsStore settings,
        Notifier notifier)
    {
        Reader = reader;
        Session = session;
        Commands = commands;
        Details = details;
        Wsl = wsl;
        WslConfig = wslConfig;
        WslcSettings = wslcSettings;
        Shell = shell;
        Settings = settings;
        Notifier = notifier;
        Monitor = new RuntimeMonitor(reader, session, details, hosts);
        Actions = new RuntimeActions(commands, wsl, reader, session, Monitor, notifier);
        Containers = new ContainerActions(commands);
        Cleanup = new SystemCleanup(commands, Actions);
    }

    public IRuntimeReader Reader { get; }

    public IRuntimeSession Session { get; }

    public IRuntimeCommands Commands { get; }

    public IRuntimeDetails Details { get; }

    public IWslPlatform Wsl { get; }

    public IWslConfig WslConfig { get; }

    public IWslcSettings WslcSettings { get; }

    public IShellLauncher Shell { get; }

    public ISettingsStore Settings { get; }

    public Notifier Notifier { get; }

    public RuntimeMonitor Monitor { get; }

    public RuntimeActions Actions { get; }

    public ContainerActions Containers { get; }

    public SystemCleanup Cleanup { get; }
}
