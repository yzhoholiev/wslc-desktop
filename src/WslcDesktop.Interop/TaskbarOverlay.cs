using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WslcDesktop.Interop;

/// <summary>
///     A badge on a window's taskbar button, shown again whenever Explorer recreates the button.
/// </summary>
public sealed unsafe class TaskbarOverlay : IDisposable
{
    /// <summary>
    ///     Explorer sends this when it creates the window's taskbar button, which drops a badge set before.
    /// </summary>
    private static readonly uint TaskbarButtonCreatedMessage = PInvoke.RegisterWindowMessage("TaskbarButtonCreated");

    private readonly HWND _hwnd;
    private readonly WindowSubclass _subclass;
    private string _description = "";
    private string? _iconPath;

    /// <summary>
    ///     Starts keeping the window's taskbar button badged; it shows nothing until <see cref="SetIcon" />.
    /// </summary>
    /// <param name="hwnd">The window whose taskbar button gets the badge.</param>
    /// <param name="onFailure">Reports an exception thrown while handling Explorer's message.</param>
    public TaskbarOverlay(nint hwnd, Action<Exception> onFailure)
    {
        _hwnd = new HWND(hwnd);
        _subclass = new WindowSubclass(hwnd, Handle, onFailure);
        _subclass.Allow(TaskbarButtonCreatedMessage);
    }

    public void Dispose() => _subclass.Dispose();

    /// <summary>
    ///     Badges the taskbar button with the .ico file; the description is what screen readers say for it.
    /// </summary>
    public void SetIcon(string iconPath, string description)
    {
        if (iconPath == _iconPath)
        {
            return;
        }

        _iconPath = iconPath;
        _description = description;
        Apply();
    }

    private bool Handle(uint message, nuint wParam, nint lParam)
    {
        if (message == TaskbarButtonCreatedMessage)
        {
            Apply();
        }

        return false;
    }

    private void Apply()
    {
        if (_iconPath is null || TaskbarList.CreateInstance(out ITaskbarList3* taskbar).Failed)
        {
            return;
        }

        try
        {
            taskbar->HrInit();
            using var icon = SafeIconHandle.FromFile(_iconPath, SafeIconHandle.SmallIconSize(PInvoke.GetDpiForWindow(_hwnd)));
            fixed (char* description = _description)
            {
                // The taskbar keeps its own copy of the icon.
                taskbar->SetOverlayIcon(_hwnd, new HICON(icon.DangerousGetHandle()), description);
            }
        }
        catch (COMException)
        {
            // Fails while the window has no button yet; TaskbarButtonCreated applies the badge once it has one.
        }
        finally
        {
            taskbar->Release();
        }
    }
}
