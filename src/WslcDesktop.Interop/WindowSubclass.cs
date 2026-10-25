using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WslcDesktop.Services;

namespace WslcDesktop.Interop;

/// <summary>
///     Hooks a window's message loop and offers each message to a handler before the window sees it.
/// </summary>
internal sealed unsafe class WindowSubclass : IDisposable
{
    private readonly Func<uint, nuint, nint, bool> _handle;
    private readonly HWND _hwnd;
    private readonly nuint _id;
    private readonly Action<Exception> _onFailure;
    private bool _disposed;
    private GCHandle _self;

    /// <summary>
    ///     Starts offering the window's messages to the handler.
    /// </summary>
    /// <param name="hwnd">The window whose messages are hooked.</param>
    /// <param name="id">Tells this hook apart from others on the same window.</param>
    /// <param name="handle">Gets the message, wParam and lParam; returns true when it handled the message, which then goes no further.</param>
    /// <param name="onFailure">Reports an exception the handler threw, after it is logged; it must not throw.</param>
    public WindowSubclass(nint hwnd, nuint id, Func<uint, nuint, nint, bool> handle, Action<Exception> onFailure)
    {
        _hwnd = new HWND(hwnd);
        _id = id;
        _handle = handle;
        _onFailure = onFailure;
        _self = GCHandle.Alloc(this);
        PInvoke.SetWindowSubclass(_hwnd, &SubclassProc, _id, (nuint)GCHandle.ToIntPtr(_self));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PInvoke.RemoveWindowSubclass(_hwnd, &SubclassProc, _id);
        _self.Free();
    }

    /// <summary>
    ///     Lets lower-integrity processes, such as Explorer or a normal second launch, send the message when the window is elevated.
    /// </summary>
    public void Allow(uint message) => PInvoke.ChangeWindowMessageFilterEx(_hwnd, message, WINDOW_MESSAGE_FILTER_ACTION.MSGFLT_ALLOW);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT SubclassProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam, nuint subclassId, nuint refData) =>
        GCHandle.FromIntPtr((nint)refData).Target is WindowSubclass subclass && subclass.TryHandle(message, wParam.Value, lParam.Value)
            ? new LRESULT(0)
            : PInvoke.DefSubclassProc(hwnd, message, wParam, lParam);

    private bool TryHandle(uint message, nuint wParam, nint lParam)
    {
        // An exception escaping into native code would terminate the process without a crash log.
        try
        {
            return _handle(message, wParam, lParam);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            _onFailure(ex);
            return true;
        }
    }
}
