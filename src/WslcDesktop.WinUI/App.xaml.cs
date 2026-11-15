using Microsoft.UI.Xaml;
using WslcDesktop.Services;

namespace WslcDesktop;

public partial class App
{
    private MainWindow? _window;

    public App()
    {
        UnhandledException += (_, e) => CrashLog.Write(e.Exception);
        try
        {
            InitializeComponent();
            Services = DefaultServices.Create();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            throw;
        }
    }

    public static MainWindow Window => ((App)Current)._window!;

    /// <summary>
    ///     The process's services, for the controls XAML creates without arguments.
    /// </summary>
    public static AppServices Services { get; private set; } = null!;

    public static bool StartInTray { get; set; }

    /// <summary>
    ///     Whether this launch is the restart that ends an update, which leaves the runtime as it is.
    /// </summary>
    public static bool IsUpdateRestart { get; set; }

    /// <summary>
    ///     What the launch asked for, run once the window exists.
    /// </summary>
    public static LaunchCommand Command { get; set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow(Services);
            // The XAML-generated Main installs the UI thread's context before it creates the app.
            SynchronizationContext context = SynchronizationContext.Current
                ?? throw new InvalidOperationException("The UI thread has no synchronization context.");

            Services.Monitor.Start(context);
            Services.Updater.Start();

            if (StartInTray)
            {
                _window.HideToTray();
            }
            else
            {
                _window.ShowAndActivate();
            }

            // A command that acts on the runtime would be refused while the start holds it.
            if (Services.Settings.Current.StartRuntimeOnLaunch && !IsUpdateRestart &&
                Command is not (LaunchCommand.StartRuntime or LaunchCommand.StopRuntime or LaunchCommand.Cleanup))
            {
                _ = Services.Actions.StartRuntimeInBackgroundAsync();
            }

            if (Command != LaunchCommand.None)
            {
                _window.Execute(Command);
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            throw;
        }
    }
}
