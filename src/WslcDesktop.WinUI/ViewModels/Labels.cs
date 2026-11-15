using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

/// <summary>
///     Short texts shown for runtime state, the same in every view.
/// </summary>
public static class Labels
{
    /// <summary>
    ///     Where the app stands with its own updates, as Settings shows it.
    /// </summary>
    public static string UpdateStatus(UpdateState state) => state.Status switch
    {
        Models.UpdateStatus.Unavailable => "Updates are available only in the released app.",
        Models.UpdateStatus.NotChecked => "Not checked yet",
        Models.UpdateStatus.Checking => "Checking for updates…",
        Models.UpdateStatus.Available => $"Version {state.Version} is available",
        Models.UpdateStatus.Downloading => $"Downloading… {Percent(state.Progress)}%",
        Models.UpdateStatus.RestartRequired => $"Restart to finish updating to {state.Version}",
        Models.UpdateStatus.Failed => state.Message ?? "The update failed.",
        _ => "Up to date"
    };

    /// <summary>
    ///     The notification area menu's entry for an update, or null when there is nothing to install.
    /// </summary>
    public static string? UpdateMenuText(UpdateState state) => state switch
    {
        { Status: Models.UpdateStatus.Available or Models.UpdateStatus.Failed, Version: { } version } => $"Update to {version}",
        { Status: Models.UpdateStatus.Downloading } => $"Downloading update… {Percent(state.Progress)}%",
        { Status: Models.UpdateStatus.RestartRequired } => "Restart to finish updating",
        _ => null
    };

    private static int Percent(double fraction) => (int)Math.Round(Math.Clamp(fraction, 0, 1) * 100);

    public static string StartStopLabel(bool isRunning) => isRunning ? "Stop" : "Start";

    public static string InUseLabel(int containerCount) => containerCount == 1 ? "In use" : $"In use · {containerCount}";

    public static string UsageLabel(int containerCount) =>
        containerCount == 0 ? "Unused" : $"Used by {Humanize.Count(containerCount, "container")}";

    public static string StateLabel(string state) => state.Length == 0 ? "" : char.ToUpperInvariant(state[0]) + state[1..];

    /// <summary>
    ///     The runtime's state in words, with the container count while it runs.
    /// </summary>
    public static string RuntimeStatus(RuntimeSnapshot snapshot) => snapshot switch
    {
        { IsWslcMissing: true } => "wslc not installed",
        { Error: not null } => "wslc unavailable",
        { IsRunning: true, Containers.Count: 0 } => "Running · no containers",
        { IsRunning: true } => $"Running · {snapshot.RunningContainers} of {Humanize.Count(snapshot.Containers.Count, "container")} up",
        { Info: not null } => "Runtime stopped",
        _ => "Connecting…"
    };

    public static string SideName(bool isAdministrator) => isAdministrator ? "Administrator" : "User";

    public static string CurrentSideTooltip(bool isAdministrator, string status) => $"{SideName(isAdministrator)} runtime: {status}";

    /// <summary>
    ///     The action that reopens the app on the given side; the ellipsis marks the permission prompt that elevating shows.
    /// </summary>
    public static string ReopenLabel(bool isAdministrator) => $"Reopen as {SideName(isAdministrator)}{(isAdministrator ? "…" : "")}";

    public static string OtherSideTooltip(bool isAdministrator, bool? isRunning)
    {
        string state = isRunning switch
        {
            true => "running",
            false => "stopped",
            null => "status unknown"
        };

        string prompt = isAdministrator ? " (asks for permission)" : "";
        return $"{CurrentSideTooltip(isAdministrator, state)}\nClick to reopen as {SideName(isAdministrator)}{prompt}";
    }
}
