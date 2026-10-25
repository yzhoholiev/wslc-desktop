using Windows.UI.ViewManagement;

namespace WslcDesktop.Controls;

/// <summary>
///     Whether a Windows contrast theme is on, and when that changes.
/// </summary>
internal static class ContrastTheme
{
    private static readonly AccessibilitySettings Accessibility = new();

    private static readonly UISettings Colors = new();

    // AccessibilitySettings.HighContrastChanged needs a CoreWindow and fails with 0x80070490 in a desktop app.
    static ContrastTheme() => Colors.ColorValuesChanged += (_, _) => Changed?.Invoke();

    /// <summary>
    ///     Whether a contrast theme is on, whose colors the user chose and content coloring would override.
    /// </summary>
    public static bool IsOn => Accessibility.HighContrast;

    /// <summary>
    ///     Raised off the UI thread when system colors change, which includes a contrast theme switching on or off.
    /// </summary>
    public static event Action? Changed;
}
