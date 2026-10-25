using Microsoft.UI.Xaml.Controls;
using WslcDesktop.Services;

namespace WslcDesktop;

/// <summary>
///     Shows the services' notification severities in WinUI's notification bar.
/// </summary>
internal static class NotificationSeverities
{
    public static InfoBarSeverity ToInfoBar(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.Error => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Success
    };
}
