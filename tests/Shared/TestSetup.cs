using System.Globalization;
using System.Runtime.CompilerServices;

namespace WslcDesktop.Tests;

internal static class TestSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Sizes and percentages format with the current culture; the expected strings are written for the invariant one.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }
}
