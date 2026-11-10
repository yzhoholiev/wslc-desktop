using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using WslcDesktop.Interop;
using WslcDesktop.Models;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop;

public sealed partial class MainWindow
{
    /// <summary>
    ///     The pages a launch command opens, so a switch to the other side can reopen the page this window shows.
    /// </summary>
    private static readonly (LaunchCommand Command, string Tag)[] PageCommands =
    [
        (LaunchCommand.ShowContainers, "containers"),
        (LaunchCommand.ShowImages, "images"),
        (LaunchCommand.ShowVolumes, "volumes"),
        (LaunchCommand.ShowWsl, "wsl")
    ];

    private readonly TaskbarOverlay? _overlay;
    private SideIndicators? _shownSides;

    private Button CurrentSideButton => Elevation.IsElevated ? AdministratorSideButton : UserSideButton;

    private Button OtherSideButton => Elevation.IsElevated ? UserSideButton : AdministratorSideButton;

    /// <summary>
    ///     The launch command that reopens a page, or <see cref="LaunchCommand.None" /> for the pages that reopen on the dashboard.
    /// </summary>
    internal static LaunchCommand CommandForPage(string tag) => PageCommands.FirstOrDefault(page => page.Tag == tag).Command;

    internal static string? PageFor(LaunchCommand command) => PageCommands.FirstOrDefault(page => page.Command == command).Tag;

    /// <summary>
    ///     The name the dot and shield styles share for a state, such as SuccessDotStyle and SuccessShieldStyle.
    /// </summary>
    private static string Tone(RuntimeState state) => state switch
    {
        RuntimeState.Running => "Success",
        RuntimeState.Error => "Critical",
        _ => "Neutral"
    };

    /// <summary>
    ///     Shows whether each side's runtime is running, and on this side the operation in progress.
    /// </summary>
    private void ShowSides(RuntimeSnapshot snapshot, string status)
    {
        string? operation = Services.Monitor.Operation;
        string current = operation is null ? Tone(snapshot.State) : "Caution";
        string other = snapshot.IsOtherSideRunning == true ? "Success" : "Neutral";
        (string user, string administrator) = Elevation.IsElevated ? (other, current) : (current, other);
        var sides = new SideIndicators(
            user,
            administrator,
            Labels.CurrentSideTooltip(Elevation.IsElevated, operation is null ? status : $"{operation}…"),
            Labels.OtherSideTooltip(!Elevation.IsElevated, snapshot.IsOtherSideRunning));

        if (sides == _shownSides)
        {
            return;
        }

        _shownSides = sides;
        UserSideDot.Style = Resource($"{user}DotStyle");
        AdministratorSideShield.Style = Resource($"{administrator}ShieldStyle");
        ToolTipService.SetToolTip(CurrentSideButton, sides.CurrentTooltip);
        ToolTipService.SetToolTip(OtherSideButton, sides.OtherTooltip);

        // A tooltip alone leaves the status out of reach of screen readers.
        AutomationProperties.SetHelpText(CurrentSideButton, sides.CurrentTooltip);
        AutomationProperties.SetHelpText(OtherSideButton, sides.OtherTooltip);
    }

    private static Style Resource(string key) => (Style)Application.Current.Resources[key];

    /// <summary>
    ///     Marks the elevated instance's tray icon and taskbar button with a shield in its runtime's state color, as the tray menu's dots.
    /// </summary>
    private void ShowAdministratorBadge(RuntimeState state)
    {
        if (_overlay is null)
        {
            return;
        }

        string name = BadgeName(state);
        _tray.SetIcon(AdministratorIcon("tray", name));
        _overlay.SetIcon(AdministratorIcon("overlay", name), $"Administrator runtime {name}");
    }

    private static string BadgeName(RuntimeState state) => state switch
    {
        RuntimeState.Running => "running",
        RuntimeState.Error => "error",
        _ => "stopped"
    };

    private static string AdministratorIcon(string kind, string badge) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Administrator", $"{kind}-{badge}.ico");

    private async void OnSideClick(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, OtherSideButton))
        {
            await SwitchSideAsync();
        }
    }

    /// <summary>
    ///     Opens the app on the other side, on the page this window shows, and quits this instance without stopping its runtime.
    /// </summary>
    private async Task SwitchSideAsync()
    {
        if (Services.Monitor.Operation is { } operation)
        {
            Services.Notifier.Error("Switch after the current operation", $"{operation} is still in progress.");
            return;
        }

        LaunchCommand command = CommandForPage(_currentTag);
        string[] arguments = command == LaunchCommand.None ? [] : [LaunchCommands.ArgumentOf(command)];
        try
        {
            if (Elevation.IsElevated)
            {
                ProcessLauncher.StartAsShellUser(Launcher.Executable, arguments);
            }
            // The consent prompt blocks the call that shows it, so it runs off the UI thread.
            else if (!await Task.Run(() => ProcessLauncher.TryStartElevated(Launcher.Executable, arguments)))
            {
                return;
            }

            await ExitApplicationAsync(keepRuntime: true);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Services.Notifier.Error($"Could not reopen as {Labels.SideName(!Elevation.IsElevated)}", ex.Message);
        }
    }

    /// <summary>
    ///     What the title bar switch shows, kept to skip updates that would change nothing.
    /// </summary>
    private sealed record SideIndicators(string UserTone, string AdministratorTone, string CurrentTooltip, string OtherTooltip);
}
