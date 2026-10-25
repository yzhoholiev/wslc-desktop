using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WslcDesktop.Interop;

/// <summary>
///     A point in physical screen pixels.
/// </summary>
public readonly record struct ScreenPoint(int X, int Y);

/// <summary>
///     A window's restored bounds in workspace coordinates, and whether restoring a minimized window maximizes it.
/// </summary>
public readonly record struct NormalPlacement(int Left, int Top, int Width, int Height, bool RestoresMaximized);

/// <summary>
///     A window's DPI and restored placement.
/// </summary>
public static class WindowGeometry
{
    /// <summary>
    ///     The window's DPI relative to 96, which a per-monitor DPI-aware process scales its pixel sizes by.
    /// </summary>
    public static double GetScale(nint hwnd) => PInvoke.GetDpiForWindow(new HWND(hwnd)) / 96.0;

    /// <summary>
    ///     The restored (normal) placement, also while the window is maximized, minimized or hidden.
    /// </summary>
    public static NormalPlacement? GetNormalPlacement(nint hwnd)
    {
        var placement = new WINDOWPLACEMENT { length = (uint)Unsafe.SizeOf<WINDOWPLACEMENT>() };
        if (!PInvoke.GetWindowPlacement(new HWND(hwnd), ref placement))
        {
            return null;
        }

        RECT normal = placement.rcNormalPosition;
        return new NormalPlacement(
            normal.left,
            normal.top,
            normal.right - normal.left,
            normal.bottom - normal.top,
            placement.flags.HasFlag(WINDOWPLACEMENT_FLAGS.WPF_RESTORETOMAXIMIZED));
    }
}
