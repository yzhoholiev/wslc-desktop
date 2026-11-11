using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using WslcDesktop.Services;

namespace WslcDesktop.Controls;

/// <summary>
///     Puts text on the clipboard for the details panes' copy buttons.
/// </summary>
internal static class ClipboardText
{
    public static void Copy(string text, Notifier notifier)
    {
        var package = new DataPackage();
        package.SetText(text);
        try
        {
            Clipboard.SetContent(package);
        }
        catch (COMException ex)
        {
            // Another process holding the clipboard open makes the call fail.
            notifier.Error("Copy failed", ex.Message);
        }
    }
}
