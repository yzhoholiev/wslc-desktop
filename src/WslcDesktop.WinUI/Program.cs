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

    private static Mutex? _instanceMutex;

    public static uint ActivateMessage { get; } = WindowMessages.Register($"WslcDesktop.Activate{Elevation.InstanceSuffix}");

    [STAThread]
    private static int Main(string[] args)
    {
        CrashLog.UseSink(CrashLogFile.Write);
        bool startInTray = args.Contains(LaunchCommands.TrayArgument, StringComparer.OrdinalIgnoreCase);
        LaunchCommand command = LaunchCommands.Parse(args);
        AppIdentity.Register();

        if (!TakeInstance())
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
        App.IsUpdateRestart = args.Contains(LaunchCommands.UpdatedArgument, StringComparer.OrdinalIgnoreCase);
        App.Command = command;
        XamlGeneratedProgram.XamlGeneratedMain();
        ReleaseInstance();

        return 0;
    }

    /// <summary>
    ///     Makes this process the single instance, and returns false when another one already is.
    /// </summary>
    public static bool TakeInstance()
    {
        if (_instanceMutex is not null)
        {
            return true;
        }

        var mutex = new Mutex(true, InstanceMutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            mutex.Dispose();
            return false;
        }

        _instanceMutex = mutex;
        return true;
    }

    /// <summary>
    ///     Lets the next launch become the instance, for a restart that replaces this process.
    /// </summary>
    public static void ReleaseInstance()
    {
        if (Interlocked.Exchange(ref _instanceMutex, null) is { } mutex)
        {
            // A mutex belongs to the thread that took it, which is the main thread here and in TakeInstance.
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}
