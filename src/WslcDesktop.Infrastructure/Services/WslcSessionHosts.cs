using System.ComponentModel;
using System.Diagnostics;

namespace WslcDesktop.Services;

/// <summary>
///     Finds the wslcsession.exe processes that wslservice starts for each wslc session.
/// </summary>
internal sealed class WslcSessionHosts : ISessionHosts
{
    public int[] ListProcessIds()
    {
        try
        {
            Process[] processes = Process.GetProcessesByName("wslcsession");
            try
            {
                return [.. processes.Select(process => process.Id).Order()];
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return [];
        }
    }
}
