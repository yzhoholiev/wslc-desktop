using WslcDesktop.Interop;
using WslcDesktop.Services;

namespace WslcDesktop;

/// <summary>
///     Entry point that keeps the app to one instance per user and elevation, and brings that instance forward on a second launch.
/// </summary>
public static class Program
{
    /// <summary>
    ///     Per user session but not per exe path, unlike WinAppSDK's AppInstance keys, so an updated version cannot run beside the old one.
    /// </summary>
    private static readonly string InstanceMutexName = $@"Local\WslcDesktop.SingleInstance{Elevation.InstanceSuffix}";

    public static uint ActivateMessage { get; } = WindowMessages.Register($"WslcDesktop.Activate{Elevation.InstanceSuffix}");

    [STAThread]
    private static int Main(string[] args)
    {
        CrashLog.UseSink(CrashLogFile.Write);
        bool startInTray = args.Contains(LaunchCommands.TrayArgument, StringComparer.OrdinalIgnoreCase);
        LaunchCommand command = LaunchCommands.Parse(args);
        AppIdentity.Register();

        using var instanceMutex = new Mutex(true, InstanceMutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            if (startInTray)
            {
                return 0;
            }

            // Windows only lets the process the user just started take the foreground, so hand that right over.
            Foreground.AllowAnyProcess();
            WindowMessages.Broadcast(ActivateMessage, (nuint)command);

            return 0;
        }

        App.StartInTray = startInTray;
        App.Command = command;
        XamlGeneratedProgram.XamlGeneratedMain();

        return 0;
    }
}
