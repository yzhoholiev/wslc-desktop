using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace WslcDesktop.ViewModels;

/// <summary>
///     Pure functions called from x:Bind expressions.
/// </summary>
public static class Bindings
{
    public static bool Not(bool value) => !value;

    public static bool CanAct(bool isRunning, bool isBusy) => isRunning && !isBusy;

    /// <summary>
    ///     Whether an empty list's action can run; only starting the runtime waits for other work to finish.
    /// </summary>
    public static bool CanRunEmptyAction(EmptyText empty, bool isBusy) => empty.Reason != EmptyReason.RuntimeStopped || !isBusy;

    public static Visibility CollapsedWhen(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility VisibleWhenAny(string? text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

    public static Style DistroStateDotStyle(string state) => StateDotStyle(state.ToLowerInvariant());

    public static string StartStopGlyph(bool isRunning) => isRunning ? Glyphs.Stop : Glyphs.Start;

    public static Style StateDotStyle(string state) => (Style)Application.Current.Resources[StateTone(state) + "DotStyle"];

    public static Style StatePillStyle(string state) => (Style)Application.Current.Resources[StateTone(state) + "PillStyle"];

    public static Style RuntimeDotStyle(bool isRunning) => StateDotStyle(isRunning ? "running" : "exited");

    /// <summary>
    ///     The color of a state's dot, for menus built each time they open.
    /// </summary>
    public static Brush StateBrush(string state) => (Brush)Application.Current.Resources[$"SystemFillColor{StateTone(state)}Brush"];

    /// <summary>
    ///     Star width of the filled part of a bar whose total width is 1*.
    /// </summary>
    public static GridLength Filled(double fraction) => new(Math.Clamp(fraction, 0, 1), GridUnitType.Star);

    /// <summary>
    ///     Star width of the empty remainder of a bar filled by <see cref="Filled" />.
    /// </summary>
    public static GridLength Remaining(double fraction) => new(1 - Math.Clamp(fraction, 0, 1), GridUnitType.Star);

    /// <summary>
    ///     The tone a container state is shown in, which names its dot and pill styles and its system fill colors.
    /// </summary>
    internal static string StateTone(string state) => state switch
    {
        "running" => "Success",
        "paused" or "restarting" => "Caution",
        "dead" => "Critical",
        _ => "Neutral"
    };
}
