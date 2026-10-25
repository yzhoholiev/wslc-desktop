namespace WslcDesktop.Controls;

/// <summary>
///     One entry of the tray menu; DotColor is a 0xRRGGBB status dot shown as its icon.
/// </summary>
public sealed record TrayMenuItem(
    string Text,
    Action? OnClick = null,
    bool IsEnabled = true,
    bool IsDefault = false,
    TrayMenuItem[]? Children = null,
    string? Glyph = null,
    uint? DotColor = null)
{
    public static TrayMenuItem Separator { get; } = new("-");

    public bool IsSeparator => ReferenceEquals(this, Separator);
}
