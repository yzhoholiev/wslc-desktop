using Windows.Win32;
using Windows.Win32.Foundation;

namespace WslcDesktop.Interop;

/// <summary>
///     Registered window messages, which processes use to reach each other's windows.
/// </summary>
public static class WindowMessages
{
    /// <summary>
    ///     The message ID for a name, the same in every process that registers it.
    /// </summary>
    public static uint Register(string name) => PInvoke.RegisterWindowMessage(name);

    /// <summary>
    ///     Posts a message to every top-level window.
    /// </summary>
    public static void Broadcast(uint message, nuint wParam) => PInvoke.PostMessage(HWND.HWND_BROADCAST, message, new WPARAM(wParam), default);
}
