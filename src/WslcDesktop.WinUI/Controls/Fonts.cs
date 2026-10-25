using Microsoft.UI.Xaml.Media;

namespace WslcDesktop.Controls;

/// <summary>
///     Fonts for controls built in code, which cannot cast App.xaml's font resources under Native AOT.
/// </summary>
public static class Fonts
{
    public static FontFamily Mono { get; } = new("JetBrains Mono, Cascadia Mono, Consolas");
}
