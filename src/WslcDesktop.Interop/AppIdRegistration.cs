using Microsoft.Win32;
using System.Security;
using WslcDesktop.Services;

namespace WslcDesktop.Interop;

/// <summary>
///     The name and icon notifications show for an app ID, which otherwise show the bare ID in their header.
/// </summary>
internal static class AppIdRegistration
{
    public static void Register(string appId, string displayName, string iconPath)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{appId}");
            SetIfChanged(key, "DisplayName", displayName);
            SetIfChanged(key, "IconUri", iconPath);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            CrashLog.Write(ex);
        }
    }

    private static void SetIfChanged(RegistryKey key, string name, string value)
    {
        if (!string.Equals(key.GetValue(name) as string, value, StringComparison.Ordinal))
        {
            key.SetValue(name, value);
        }
    }
}
