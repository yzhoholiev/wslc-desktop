using System.Runtime.CompilerServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace WslcDesktop.Interop;

/// <summary>
///     Taking and handing over the foreground window.
/// </summary>
public static class Foreground
{
    /// <summary>
    ///     Lets any process take the foreground, as the process the user just started may hand that right to another.
    /// </summary>
    public static void AllowAnyProcess() => PInvoke.AllowSetForegroundWindow(PInvoke.ASFW_ANY);

    /// <summary>
    ///     Asks for the foreground, which Windows grants only to the process that last received input.
    /// </summary>
    public static void Request(nint hwnd) => PInvoke.SetForegroundWindow(new HWND(hwnd));

    /// <summary>
    ///     Takes the foreground for a user-requested activation, which Windows' foreground lock would otherwise turn into a taskbar flash.
    /// </summary>
    public static void BringToFront(nint hwnd)
    {
        var window = new HWND(hwnd);
        if (PInvoke.SetForegroundWindow(window) && PInvoke.GetForegroundWindow() == window)
        {
            return;
        }

        // An empty input event makes this process the last to receive input, which lifts the lock.
        INPUT[] input = [new() { type = INPUT_TYPE.INPUT_MOUSE }];
        PInvoke.SendInput(input, Unsafe.SizeOf<INPUT>());
        PInvoke.SetForegroundWindow(window);
    }
}
