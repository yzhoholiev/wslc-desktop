using WslcDesktop.Services;

namespace WslcDesktop.Interop;

/// <summary>
///     The app's taskbar identity: its app ID and name, and the launcher that pinned buttons and jump list tasks start.
/// </summary>
internal static class AppIdentity
{
    public const string DisplayName = "WSLC Desktop";

    /// <summary>
    ///     Shared by every unpacked version, so pins and the jump list survive upgrades; elevated instances group on their own.
    /// </summary>
    private static readonly string AppId = $"WslcDesktop{Elevation.InstanceSuffix}";

    /// <summary>
    ///     The name this instance shows; an elevated one has its own window, tray icon and taskbar group, which must tell the two apart.
    /// </summary>
    public static string Name { get; } = Elevation.IsElevated ? $"{DisplayName} (Administrator)" : DisplayName;

    /// <summary>
    ///     Gives the process its taskbar and notification identity; it must run before the first window is created.
    /// </summary>
    public static void Register() =>
        Taskbar.SetProcessAppId(AppId, Name, Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.png"));

    /// <summary>
    ///     Makes a pinned taskbar button start the launcher instead of the unpacked version that happened to be running.
    /// </summary>
    public static void SetRelaunchTarget(nint hwnd)
    {
        if (Launcher.Path is { } launcher)
        {
            Taskbar.SetRelaunchTarget(hwnd, AppId, $"\"{launcher}\"", DisplayName, $"{launcher},0");
        }
    }

    public static void ClearRelaunchTarget(nint hwnd)
    {
        if (Launcher.Path is not null)
        {
            Taskbar.ClearRelaunchTarget(hwnd);
        }
    }

    public static void SetJumpListTasks()
    {
        // Tasks start a normal instance, so an elevated one would get a list that acts on a different session.
        if (!Elevation.IsElevated)
        {
            JumpList.SetTasks(AppId, JumpListTasks.All);
        }
    }
}
