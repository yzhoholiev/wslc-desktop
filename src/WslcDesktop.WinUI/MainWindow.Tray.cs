using Windows.Graphics;
using WslcDesktop.Controls;
using WslcDesktop.Interop;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop;

public sealed partial class MainWindow
{
    private const int MaxTrayContainers = 25;

    private const uint RunningDot = 0x3FB950;
    private const uint StoppedDot = 0x8A8A8A;
    private const uint ErrorDot = 0xE5484D;

    /// <summary>
    ///     wslc info answers in well under 100 ms, so the menu opens with the current state yet never waits long on a hung wslc.
    /// </summary>
    private static readonly TimeSpan TrayRefreshWait = TimeSpan.FromMilliseconds(300);

    private async void OnTrayMenuRequested(ScreenPoint cursor)
    {
        try
        {
            await Task.WhenAny(RefreshIfIdleAsync(), Task.Delay(TrayRefreshWait));
            _trayMenu.Show(BuildTrayMenu(), new PointInt32(cursor.X, cursor.Y));
        }
        catch (Exception ex)
        {
            // Raised from the tray icon's window procedure, where an exception would end the app.
            CrashLog.Write(ex);
        }
    }

    private TrayMenuItem[] BuildTrayMenu()
    {
        RuntimeSnapshot snapshot = Services.Monitor.Latest;
        uint statusDot = snapshot switch
        {
            { Error: not null } => ErrorDot,
            { IsRunning: true } => RunningDot,
            _ => StoppedDot
        };

        TrayMenuItem[] runtimeItems = snapshot.IsRunning
            ?
            [
                new TrayMenuItem("Clean up…", () => _ = ShowDashboard().CleanupAsync(), Glyph: Glyphs.Cleanup),
                new TrayMenuItem("Stop container runtime…", () => _ = ShowDashboard().StopRuntimeAsync(), Glyph: Glyphs.Shutdown)
            ]
            :
            [
                new TrayMenuItem("Start container runtime", () => _ = Services.Actions.StartRuntimeInBackgroundAsync(), Glyph: Glyphs.Start)
            ];

        return
        [
            new TrayMenuItem(RuntimeText.Text, IsEnabled: false, DotColor: statusDot),
            TrayMenuItem.Separator,
            new TrayMenuItem($"Open {AppIdentity.DisplayName}", ShowAndActivate, IsDefault: true, Glyph: Glyphs.Open),
            TrayMenuItem.Separator,
            new TrayMenuItem("Containers", IsEnabled: snapshot is { IsRunning: true, Error: null }, Children: BuildContainerItems(snapshot),
                Glyph: Glyphs.Containers),
            .. runtimeItems,
            Services.Monitor.IsLive
                ? new TrayMenuItem("Pause live updates", () => SetLive(false), Glyph: Glyphs.Pause)
                : new TrayMenuItem("Resume live updates", () => SetLive(true), Glyph: Glyphs.Start),
            TrayMenuItem.Separator,
            new TrayMenuItem("WSL", Children:
            [
                new TrayMenuItem("Shut down WSL…", () => _ = ShowDashboard().ShutdownWslAsync(), Glyph: Glyphs.Shutdown),
                new TrayMenuItem("Update WSL", () => RunInBackground(new UserOperation("Updating WSL", () => Services.Actions.UpdateWslAsync(false))),
                    Glyph: Glyphs.Update),
                new TrayMenuItem("Open WSL Settings", () => Services.Shell.OpenWslSettings(), Services.Wsl.IsSettingsAppInstalled,
                    Glyph: Glyphs.Settings),
                new TrayMenuItem("Edit .wslconfig", Services.Shell.OpenWslConfig, Glyph: Glyphs.Edit),
                new TrayMenuItem("Distributions and sessions", () => ShowPage("wsl"), Glyph: Glyphs.Distributions)
            ], Glyph: Glyphs.Shell),
            new TrayMenuItem("Settings", () => ShowPage("settings"), Glyph: Glyphs.Settings),
            TrayMenuItem.Separator,
            new TrayMenuItem("Exit", () => _ = ExitApplicationAsync(), Glyph: Glyphs.Exit)
        ];
    }

    private TrayMenuItem[] BuildContainerItems(RuntimeSnapshot snapshot)
    {
        var containers = snapshot.Containers
            .OrderByDescending(container => container.IsRunning)
            .ThenBy(container => container.Names, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (containers.Count == 0)
        {
            return [new TrayMenuItem("No containers", IsEnabled: false)];
        }

        List<TrayMenuItem> items = [];
        if (snapshot.RunningContainers > 0)
        {
            items.Add(new TrayMenuItem(
                "Stop all containers…",
                () => _ = ShowDashboard().StopAllContainersAsync(),
                Glyph: Glyphs.Stop));

            items.Add(TrayMenuItem.Separator);
        }

        items.AddRange(containers.Take(MaxTrayContainers).Select(BuildContainerItem));

        if (containers.Count <= MaxTrayContainers)
        {
            return [.. items];
        }

        items.Add(TrayMenuItem.Separator);
        items.Add(new TrayMenuItem($"All {containers.Count} containers…", () => ShowPage("containers"), Glyph: Glyphs.Containers));

        return [.. items];
    }

    private TrayMenuItem BuildContainerItem(ContainerDto container)
    {
        bool isRunning = container.IsRunning;
        string name = container.Names;

        return new TrayMenuItem(
            $"{name}  —  {container.Image}",
            Children:
            [
                isRunning
                    ? new TrayMenuItem("Stop", () => RunInBackground(Services.Containers.Stop(container.Id, name)), Glyph: Glyphs.Stop)
                    : new TrayMenuItem("Start", () => RunInBackground(Services.Containers.Start(container.Id, name)), Glyph: Glyphs.Start),
                new TrayMenuItem("Restart", () => RunInBackground(Services.Containers.Restart(container.Id, name)), isRunning, Glyph: Glyphs.Restart),
                new TrayMenuItem("Open shell", () => Services.Shell.OpenContainerShell(container.Id), isRunning, Glyph: Glyphs.Shell),
                new TrayMenuItem("Show logs", () => ShowContainer(container.Id), Glyph: Glyphs.Logs)
            ],
            DotColor: isRunning ? RunningDot : StoppedDot);
    }

    private void RunInBackground(UserOperation operation) => _ = RunAndReportAsync(operation);

    private async Task RunAndReportAsync(UserOperation operation)
    {
        await Services.Notifier.RunAsync(operation.Description, operation.Run, operation.SuccessMessage);

        try
        {
            await Services.Monitor.RefreshAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
        }
    }
}
