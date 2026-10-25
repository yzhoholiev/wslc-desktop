using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WslcDesktop.Interop;

/// <summary>
///     An invisible 1×1 topmost window that anchors a menu at a screen position.
/// </summary>
public static class PopupWindow
{
    /// <summary>
    ///     Turns the window into an invisible, topmost 1×1 popup without a frame or taskbar button.
    /// </summary>
    public static void MakeInvisibleToolWindow(nint hwnd)
    {
        var window = new HWND(hwnd);

        // A captioned window has a minimum size and a frame, so the host is a popup; the menu is its own window, unaffected by the transparency.
        PInvoke.SetWindowLongPtr(window, WINDOW_LONG_PTR_INDEX.GWL_STYLE, unchecked((nint)(uint)WINDOW_STYLE.WS_POPUP));
        PInvoke.SetWindowLongPtr(
            window,
            WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            (nint)(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TOPMOST));

        PInvoke.SetLayeredWindowAttributes(window, default, 0, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
        PInvoke.SetWindowPos(window, HWND.HWND_TOPMOST, 0, 0, 1, 1, SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);
    }

    /// <summary>
    ///     Moves the 1×1 popup to a screen position, keeping it topmost and inactive.
    /// </summary>
    public static void MoveTopmost(nint hwnd, int x, int y) =>
        PInvoke.SetWindowPos(new HWND(hwnd), HWND.HWND_TOPMOST, x, y, 1, 1, SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
}
