using System.Security.Principal;

namespace WslcDesktop.Services;

/// <summary>
///     Whether this process runs as administrator, which gives it its own wslc session and VM.
/// </summary>
public static class Elevation
{
    public static bool IsElevated { get; } = CheckElevated();

    /// <summary>
    ///     Appended to names one user's instances share, so an elevated instance runs beside a normal one.
    /// </summary>
    public static string InstanceSuffix => IsElevated ? ".Administrator" : "";

    private static bool CheckElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
