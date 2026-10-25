using System.Drawing;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WslcDesktop.Interop;

/// <summary>
///     Notification-area icon attached to an existing window's message loop.
/// </summary>
public sealed unsafe class TrayIcon : IDisposable
{
    private const uint CallbackMessage = PInvoke.WM_APP + 1;
    private const uint IconId = 1;
    private const nuint SubclassId = 0x5753_4C43;

    /// <summary>
    ///     Explorer broadcasts this after it restarts; tray icons must be re-added.
    /// </summary>
    private static readonly uint TaskbarCreatedMessage = PInvoke.RegisterWindowMessage("TaskbarCreated");

    private readonly HWND _hwnd;
    private readonly string _iconPath;
    private SafeIconHandle _icon;
    private int _iconSize;
    private readonly WindowSubclass _subclass;
    private bool _disposed;
    private string _tooltip;

    /// <summary>
    ///     Adds the icon to the notification area and handles its messages on the window's message loop.
    /// </summary>
    /// <param name="hwnd">The window whose message loop receives the icon's mouse and Explorer messages.</param>
    /// <param name="iconPath">Path to the .ico file the icon shows.</param>
    /// <param name="tooltip">Text shown when hovering the icon; Windows shows at most 127 characters.</param>
    /// <param name="onFailure">Reports an exception a click or menu handler threw.</param>
    public TrayIcon(nint hwnd, string iconPath, string tooltip, Action<Exception> onFailure)
    {
        _hwnd = new HWND(hwnd);
        _tooltip = tooltip;
        _iconPath = iconPath;
        _iconSize = IconSize();
        _icon = LoadIcon(_iconSize);

        _subclass = new WindowSubclass(hwnd, SubclassId, Handle, onFailure);
        _subclass.Allow(TaskbarCreatedMessage);
        Add();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NOTIFYICONDATAW data = CreateData(0);
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in data);
        _subclass.Dispose();
        _icon.Dispose();
    }

    /// <summary>
    ///     Raised on left click.
    /// </summary>
    public event Action? Activated;

    /// <summary>
    ///     Raised on right click with the cursor position in physical screen pixels.
    /// </summary>
    public event Action<ScreenPoint>? ContextMenuRequested;

    public void SetTooltip(string tooltip)
    {
        if (tooltip == _tooltip)
        {
            return;
        }

        _tooltip = tooltip;
        NOTIFYICONDATAW data = CreateData(NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP);
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in data);
    }

    public void ShowBalloon(string title, string message, bool isError = false)
    {
        NOTIFYICONDATAW data = CreateData(NOTIFY_ICON_DATA_FLAGS.NIF_INFO);
        Copy(title, data.szInfoTitle.AsSpan());
        Copy(message, data.szInfo.AsSpan());
        data.dwInfoFlags = isError ? NOTIFY_ICON_INFOTIP_FLAGS.NIIF_ERROR : NOTIFY_ICON_INFOTIP_FLAGS.NIIF_INFO;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in data);
    }

    private bool Handle(uint message, nuint wParam, nint lParam)
    {
        if (message == CallbackMessage)
        {
            switch ((uint)lParam)
            {
                case PInvoke.WM_LBUTTONUP:
                    Activated?.Invoke();
                    break;
                case PInvoke.WM_RBUTTONUP:
                    PInvoke.GetCursorPos(out Point cursor);
                    ContextMenuRequested?.Invoke(new ScreenPoint(cursor.X, cursor.Y));
                    break;
            }

            return true;
        }

        if (message == TaskbarCreatedMessage)
        {
            ReplaceIcon(IconSize());
            Add();
        }
        // The window gets WM_DPICHANGED only for its own display; a scale change on another one arrives as WM_SETTINGCHANGE.
        else if (message is PInvoke.WM_DPICHANGED or PInvoke.WM_DISPLAYCHANGE or PInvoke.WM_SETTINGCHANGE && IconSize() is var size && size != _iconSize)
        {
            ReplaceIcon(size);
            NOTIFYICONDATAW data = CreateData(NOTIFY_ICON_DATA_FLAGS.NIF_ICON);
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in data);
        }

        return false;
    }

    /// <summary>
    ///     The small-icon size for the primary display, whose taskbar holds the notification area.
    /// </summary>
    private int IconSize()
    {
        // A per-monitor DPI-aware process must scale the 16 px small-icon size itself; the .ico has frames up to 40 px.
        HMONITOR primary = PInvoke.MonitorFromPoint(default, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
        uint dpi = PInvoke.GetDpiForMonitor(primary, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out uint dpiX, out _).Succeeded ? dpiX : PInvoke.GetDpiForWindow(_hwnd);
        return (int)(16 * dpi / 96);
    }

    private SafeIconHandle LoadIcon(int size) =>
        new((nint)PInvoke.LoadImage(default, _iconPath, GDI_IMAGE_TYPE.IMAGE_ICON, size, size, IMAGE_FLAGS.LR_LOADFROMFILE).Value);

    private void ReplaceIcon(int size)
    {
        SafeIconHandle previous = _icon;
        _iconSize = size;
        _icon = LoadIcon(size);
        previous.Dispose();
    }

    private void Add()
    {
        NOTIFYICONDATAW data = CreateData(
            NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP);

        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, in data);
    }

    private NOTIFYICONDATAW CreateData(NOTIFY_ICON_DATA_FLAGS flags)
    {
        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = _hwnd,
            uID = IconId,
            uFlags = flags,
            uCallbackMessage = CallbackMessage,
            hIcon = new HICON(_icon.DangerousGetHandle())
        };

        Copy(_tooltip, data.szTip.AsSpan());
        return data;
    }

    private static void Copy(string value, Span<char> destination)
    {
        int length = Math.Min(value.Length, destination.Length - 1);
        value.AsSpan(0, length).CopyTo(destination);
        destination[length] = '\0';
    }
}
