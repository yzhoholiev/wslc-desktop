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

    protected override bool ReleaseHandle() => PInvoke.DestroyIcon(new HICON(handle));
}
