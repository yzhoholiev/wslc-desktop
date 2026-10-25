using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WslcDesktop.Controls;
using WslcDesktop.Interop;
using WslcDesktop.Services;

namespace WslcDesktop;

public sealed partial class MainWindow
{
    /// <summary>
    ///     Long enough for the dropped pages to unload and release their event subscriptions before collecting.
    /// </summary>
    private static readonly TimeSpan TrimDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     Polling keeps paging memory back in while hidden; measured at about 2.5 MB a minute.
    /// </summary>
    private static readonly TimeSpan RetrimInterval = TimeSpan.FromMinutes(5);

    private bool _isExiting;
    private DispatcherQueueTimer? _trimTimer;

    public void ShowAndActivate()
    {
        if (_maximizeOnFirstShow)
        {
            // The first activation completes asynchronously and resets the window to normal, so maximize once it has.
            _maximizeOnFirstShow = false;

            void MaximizeOnce(object sender, WindowActivatedEventArgs args)
            {
                Activated -= MaximizeOnce;
                _presenter.Maximize();
            }

            Activated += MaximizeOnce;
        }
        else if (_presenter.State == OverlappedPresenterState.Minimized)
        {
            _presenter.Restore();
        }

        _trimTimer?.Stop();
        PageHost.Child ??= GetOrCreatePage(_currentTag);

        AppWindow.Show();
        Activate();
        Foreground.BringToFront(_hwnd);
        _ = RefreshIfIdleAsync();
    }

    /// <summary>
    ///     Asks wslc at once when the user looks, so a runtime started elsewhere shows without waiting for the next rare check.
    /// </summary>
    private Task RefreshIfIdleAsync() => Services.Monitor.IsIdle ? Services.Monitor.RefreshAsync() : Task.CompletedTask;

    /// <summary>
    ///     Runs what a launch asked for: a page, or the tray's runtime and cleanup actions.
    /// </summary>
    internal void Execute(LaunchCommand command)
    {
        switch (command)
        {
            case LaunchCommand.ShowContainers:
                ShowPage("containers");
                break;
            case LaunchCommand.ShowImages:
                ShowPage("images");
                break;
            case LaunchCommand.ShowVolumes:
                ShowPage("volumes");
                break;
            case LaunchCommand.ShowWsl:
                ShowPage("wsl");
                break;
            case LaunchCommand.StartRuntime:
                _ = Services.Actions.StartRuntimeInBackgroundAsync();
                break;
            case LaunchCommand.StopRuntime:
                _ = ShowDashboard().StopRuntimeAsync();
                break;
            case LaunchCommand.Cleanup:
                _ = ShowDashboard().CleanupAsync();
                break;
            default:
                ShowAndActivate();
                break;
        }
    }

    public async Task ExitApplicationAsync()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;

        // A programmatic Close() does not raise AppWindow.Closing, where placement is otherwise saved.
        SavePlacement();
        if (Services.Settings.Current.StopRuntimeOnExit)
        {
            AppWindow.Hide();
            _tray.SetTooltip($"{AppIdentity.Name} · Stopping the container runtime…");
            await Services.Actions.StopRuntimeOnExitAsync();
        }

        Close();
        Application.Current.Exit();
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        SavePlacement();
        if (_isExiting)
        {
            return;
        }

        args.Cancel = true;
        if (!Services.Settings.Current.KeepRunningInTray)
        {
            // Exits through the same path as the tray menu, which may first stop the runtime.
            _ = ExitApplicationAsync();
            return;
        }

        HideToTray();

        if (!Services.Settings.Current.TrayHintShown)
        {
            _tray.ShowBalloon($"{AppIdentity.DisplayName} is still running", "Use the tray icon to reopen it or to manage containers. Choose Exit from its menu to quit.");
            Services.Settings.Current.TrayHintShown = true;
            Services.Settings.Save();
        }
    }

    /// <summary>
    ///     Hides the window and frees what only the window needs, since a tray session can last for days.
    /// </summary>
    public void HideToTray()
    {
        // An open dialog would act on the page dropped below once answered.
        Dialogs.HideOpen();
        AppWindow.Hide();

        // Cached pages keep their whole visual trees alive; they are rebuilt when the window is shown again.
        PageHost.Child = null;
        _pages.Clear();

        if (_trimTimer is null)
        {
            _trimTimer = DispatcherQueue.CreateTimer();
            _trimTimer.Tick += (timer, _) =>
            {
                if (AppWindow.IsVisible)
                {
                    timer.Stop();
                    return;
                }

                TrimMemory();
                timer.Interval = RetrimInterval;
            };
        }

        _trimTimer.Interval = TrimDelay;
        _trimTimer.IsRepeating = true;
        _trimTimer.Start();
    }

    private static void TrimMemory()
    {
        // Released XAML elements are freed by finalizers, so a second collection reclaims what they referenced.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);

        ProcessMemory.TrimWorkingSet();
    }
}
