using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell.PropertiesSystem;
using WslcDesktop.Services;

namespace WslcDesktop.Interop;

/// <summary>
///     The process's taskbar identity and a window's pinned relaunch target.
/// </summary>
public static unsafe class Taskbar
{
    /// <summary>
    ///     Gives the process its taskbar and notification identity; it must run before the first window is created.
    /// </summary>
    public static void SetProcessAppId(string appId, string displayName, string iconPath)
    {
        _ = PInvoke.SetCurrentProcessExplicitAppUserModelID(appId);
        AppIdRegistration.Register(appId, displayName, iconPath);
    }

    /// <summary>
    ///     Makes the window's pinned taskbar button run a command of its own instead of the process that showed the window.
    /// </summary>
    public static void SetRelaunchTarget(nint hwnd, string appId, string command, string displayName, string iconResource) =>
        UpdateWindowProperties(hwnd, store =>
        {
            ShellProperties.SetText(store, PInvoke.PKEY_AppUserModel_ID, appId);
            ShellProperties.SetText(store, PInvoke.PKEY_AppUserModel_RelaunchCommand, command);
            ShellProperties.SetText(store, PInvoke.PKEY_AppUserModel_RelaunchDisplayNameResource, displayName);
            ShellProperties.SetText(store, PInvoke.PKEY_AppUserModel_RelaunchIconResource, iconResource);
        });

    /// <summary>
    ///     Removes the window's relaunch properties, which the shell expects before the window is destroyed.
    /// </summary>
    public static void ClearRelaunchTarget(nint hwnd) =>
        UpdateWindowProperties(hwnd, store =>
        {
            ShellProperties.Clear(store, PInvoke.PKEY_AppUserModel_RelaunchIconResource);
            ShellProperties.Clear(store, PInvoke.PKEY_AppUserModel_RelaunchDisplayNameResource);
            ShellProperties.Clear(store, PInvoke.PKEY_AppUserModel_RelaunchCommand);
            ShellProperties.Clear(store, PInvoke.PKEY_AppUserModel_ID);
        });

    private static void UpdateWindowProperties(nint hwnd, Action<nint> update)
    {
        if (PInvoke.SHGetPropertyStoreForWindow(new HWND(hwnd), out IPropertyStore* store).Failed)
        {
            return;
        }

        try
        {
            update((nint)store);
        }
        catch (COMException ex)
        {
            CrashLog.Write(ex);
        }
        finally
        {
            store->Release();
        }
    }
}
