namespace WslcDesktop.Interop;

/// <summary>
///     Raises <see cref="Received" /> when a window receives a registered window message.
/// </summary>
public sealed class WindowMessageHook : IDisposable
{
    private const nuint SubclassId = 0x5753_4C44;

    private readonly uint _message;
    private readonly WindowSubclass _subclass;

    /// <summary>
    ///     Starts listening for the message on the window's message loop, also from a non-elevated sender when the window is elevated.
    /// </summary>
    /// <param name="hwnd">The window that receives the message.</param>
    /// <param name="message">A message from RegisterWindowMessage.</param>
    /// <param name="onFailure">Reports an exception a <see cref="Received" /> handler threw.</param>
    public WindowMessageHook(nint hwnd, uint message, Action<Exception> onFailure)
    {
        _message = message;
        _subclass = new WindowSubclass(hwnd, SubclassId, Handle, onFailure);
        _subclass.Allow(message);
    }

    public void Dispose() => _subclass.Dispose();

    /// <summary>
    ///     Raised with the message's wParam.
    /// </summary>
    public event Action<nuint>? Received;

    private bool Handle(uint message, nuint wParam, nint lParam)
    {
        if (message != _message)
        {
            return false;
        }

        Received?.Invoke(wParam);
        return true;
    }
}
