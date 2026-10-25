using Windows.Win32.Foundation;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace WslcDesktop.Interop;

/// <summary>
///     Writes values into a shell property store, passed as a handle because pointers cannot be generic arguments.
/// </summary>
internal static unsafe class ShellProperties
{
    public static void SetText(nint store, PROPERTYKEY key, string text)
    {
        fixed (char* chars = text)
        {
            var value = new PROPVARIANT
            {
                vt = VARENUM.VT_LPWSTR,
                pwszVal = new PWSTR(chars)
            };

            ((IPropertyStore*)store)->SetValue(&key, &value);
        }
    }

    public static void SetFlag(nint store, PROPERTYKEY key)
    {
        var value = new PROPVARIANT
        {
            vt = VARENUM.VT_BOOL,
            boolVal = VARIANT_BOOL.VARIANT_TRUE
        };

        ((IPropertyStore*)store)->SetValue(&key, &value);
    }

    public static void Clear(nint store, PROPERTYKEY key)
    {
        var empty = new PROPVARIANT();
        ((IPropertyStore*)store)->SetValue(&key, &empty);
    }
}
