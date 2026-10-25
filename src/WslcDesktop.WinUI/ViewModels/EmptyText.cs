namespace WslcDesktop.ViewModels;

/// <summary>
///     Why a list is empty, which picks the action its empty-state button runs.
/// </summary>
public enum EmptyReason
{
    None,
    Unavailable,
    RuntimeStopped,
    NoMatches,
    NoneRunning,
    NoItems
}

/// <summary>
///     What an empty list shows instead of rows, and which action its button runs.
/// </summary>
public sealed record EmptyText(EmptyReason Reason, string Glyph, string Title, string Message = "", string ActionText = "")
{
    public static EmptyText None { get; } = new(EmptyReason.None, "", "");

    public static EmptyText Unavailable(string message, bool wslcMissing) =>
        new(EmptyReason.Unavailable, Glyphs.Error, wslcMissing ? "wslc is not installed" : "Could not reach wslc", message);

    public static EmptyText RuntimeStopped(string items) => new(
        EmptyReason.RuntimeStopped,
        Glyphs.Shutdown,
        "The container runtime is stopped",
        $"It starts on demand with any container command. Start it now to see your {items}.",
        "Start runtime");

    public static EmptyText NoMatches(string search) =>
        new(EmptyReason.NoMatches, Glyphs.Search, "No matches", $"Nothing matches \"{search}\".", "Clear search");
}
