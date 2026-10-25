using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

/// <summary>
///     Short texts shown for runtime state, the same in every view.
/// </summary>
public static class Labels
{
    public static string StartStopLabel(bool isRunning) => isRunning ? "Stop" : "Start";

    public static string InUseLabel(int containerCount) => containerCount == 1 ? "In use" : $"In use · {containerCount}";

    public static string UsageLabel(int containerCount) =>
        containerCount == 0 ? "Unused" : $"Used by {Humanize.Count(containerCount, "container")}";

    public static string StateLabel(string state) => state.Length == 0 ? "" : char.ToUpperInvariant(state[0]) + state[1..];
}
