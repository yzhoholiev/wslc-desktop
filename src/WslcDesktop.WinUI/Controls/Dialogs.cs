using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WslcDesktop.Services;

namespace WslcDesktop.Controls;

public static class Dialogs
{
    /// <summary>
    ///     The dialog on screen; WinUI throws when a second ContentDialog opens while one is showing.
    /// </summary>
    private static ContentDialog? _open;

    /// <summary>
    ///     Closes the open dialog as cancelled.
    /// </summary>
    public static void HideOpen() => _open?.Hide();

    public static async Task<bool> ConfirmAsync(XamlRoot xamlRoot, string title, string message, string primaryButtonText)
    {
        ContentDialog dialog = Create(xamlRoot, title, new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, primaryButtonText);
        return await ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    public static Task<bool> ConfirmStopAllContainersAsync(XamlRoot xamlRoot, int runningContainers) => ConfirmAsync(
        xamlRoot,
        "Stop all containers?",
        $"{Humanize.Count(runningContainers, "running container")} will be stopped. They are kept and can be started again.",
        "Stop all");

    public static Task<bool> ConfirmShutdownWslAsync(XamlRoot xamlRoot) => ConfirmAsync(
        xamlRoot,
        "Shut down WSL?",
        "This immediately stops every WSL distribution, the container runtime, and all running containers.",
        "Shut down");

    internal static ContentDialog Create(XamlRoot xamlRoot, string title, object content, string primaryButtonText) => new()
    {
        XamlRoot = xamlRoot,
        Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
        Title = title,
        Content = content,
        PrimaryButtonText = primaryButtonText,
        CloseButtonText = "Cancel",
        DefaultButton = ContentDialogButton.Close
    };

    internal static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        if (_open is not null)
        {
            return ContentDialogResult.None;
        }

        _open = dialog;
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            _open = null;
        }
    }
}
