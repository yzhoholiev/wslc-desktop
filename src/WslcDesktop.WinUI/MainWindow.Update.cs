using System.ComponentModel;
using WslcDesktop.Interop;
using WslcDesktop.Services;

namespace WslcDesktop;

public sealed partial class MainWindow
{
    /// <summary>
    ///     The launcher arguments that bring the app back as it is now: in the notification area, or on the page it shows.
    /// </summary>
    internal static string[] RestartArguments(bool isHidden, string currentTag)
    {
        if (isHidden)
        {
            return [LaunchCommands.TrayArgument, LaunchCommands.UpdatedArgument];
        }

        LaunchCommand command = CommandForPage(currentTag);
        return command == LaunchCommand.None
            ? [LaunchCommands.UpdatedArgument]
            : [LaunchCommands.ArgumentOf(command), LaunchCommands.UpdatedArgument];
    }

    /// <summary>
    ///     Installs the update on offer, or restarts into an installed one.
    /// </summary>
    private async Task UpdateAppAsync()
    {
        try
        {
            await Services.Updater.UpdateAsync();
        }
        catch (Exception ex)
        {
            // Started from the notification area menu, where an exception would end the app.
            CrashLog.Write(ex);
        }
    }

    /// <summary>
    ///     Announces a newly found version in the notification area, which is all an instance without a window can show.
    /// </summary>
    private void OnUpdaterChanged()
    {
        try
        {
            if (Services.Updater.TakeAnnouncement() is { } version)
            {
                _tray.ShowBalloon($"{AppIdentity.DisplayName} {version} is available",
                    $"Open the notification area menu and choose Update to {version}.");
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
        }
    }

    /// <summary>
    ///     Starts the app again through its launcher, which now holds the new version, and quits this instance without stopping its runtime.
    /// </summary>
    private async void OnRestartRequested()
    {
        try
        {
            // An update that finishes while the app is quitting must not bring it back.
            if (_isExiting)
            {
                return;
            }

            if (Services.Monitor.Operation is { } operation)
            {
                Services.Notifier.Error("Restart after the current operation", $"{operation} is still in progress.");
                return;
            }

            // The new instance quits when it finds the single-instance mutex held, so it must be free before the launcher starts.
            Program.ReleaseInstance();
            try
            {
                Services.Installed.Restart(RestartArguments(!AppWindow.IsVisible, _currentTag));
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or UpdateException)
            {
                Program.TakeInstance();
                Services.Notifier.Error($"Could not restart {AppIdentity.DisplayName}", ex.Message);
                return;
            }

            await ExitApplicationAsync(true);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
        }
    }
}
