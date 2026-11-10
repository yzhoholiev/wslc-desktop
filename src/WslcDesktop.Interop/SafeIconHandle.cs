using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WslcDesktop.Interop;

/// <summary>
///     An icon this process loaded, destroyed when the handle is released.
/// </summary>
internal sealed class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeIconHandle(nint handle)
        : base(true)
    {
        SetHandle(handle);
    }

    /// <summary>
    ///     The small-icon size at the given DPI, which a per-monitor DPI-aware process must scale from 16 px itself.
    /// </summary>
    public static int SmallIconSize(uint dpi) => (int)(16 * dpi / 96);

    /// <summary>
    ///     Loads the frame of an .ico file that best fits the size.
    /// </summary>
    public static unsafe SafeIconHandle FromFile(string path, int size) =>
        new((nint)PInvoke.LoadImage(default, path, GDI_IMAGE_TYPE.IMAGE_ICON, size, size, IMAGE_FLAGS.LR_LOADFROMFILE).Value);

    protected override bool ReleaseHandle() => PInvoke.DestroyIcon(new HICON(handle));
}
