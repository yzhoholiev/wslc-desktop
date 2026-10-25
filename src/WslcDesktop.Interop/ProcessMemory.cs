using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace WslcDesktop.Interop;

public static class ProcessMemory
{
    /// <summary>
    ///     Moves the process's idle pages out of its working set; Windows pages back in whatever is touched again.
    /// </summary>
    public static void TrimWorkingSet()
    {
        using var process = Process.GetCurrentProcess();
        PInvoke.SetProcessWorkingSetSize(new HANDLE(process.Handle), nuint.MaxValue, nuint.MaxValue);
    }
}
