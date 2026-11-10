using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;

namespace WslcDesktop.Interop;

/// <summary>
///     Starts a program at the other elevation: elevated through the consent prompt, or unelevated from an elevated process.
/// </summary>
public static class ProcessLauncher
{
    private const int ErrorCancelled = 1223;

    private const TOKEN_ACCESS_MASK PrimaryTokenAccess = TOKEN_ACCESS_MASK.TOKEN_QUERY | TOKEN_ACCESS_MASK.TOKEN_DUPLICATE |
                                                         TOKEN_ACCESS_MASK.TOKEN_ASSIGN_PRIMARY | TOKEN_ACCESS_MASK.TOKEN_ADJUST_DEFAULT |
                                                         TOKEN_ACCESS_MASK.TOKEN_ADJUST_SESSIONID;

    /// <summary>
    ///     Starts the program elevated, which shows the consent prompt; false when the user declines it.
    /// </summary>
    public static bool TryStartElevated(string path, ReadOnlySpan<string> arguments)
    {
        var startInfo = new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(path)
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }
    }

    /// <summary>
    ///     Starts the program with the token of the desktop shell, so an elevated process can start one at the signed-in user's normal level.
    /// </summary>
    public static unsafe void StartAsShellUser(string path, ReadOnlySpan<string> arguments)
    {
        HWND shell = PInvoke.GetShellWindow();
        if (shell.IsNull)
        {
            throw new InvalidOperationException("The Windows shell is not running, so there is no unelevated session to start the app in.");
        }

        uint shellProcessId;
        PInvoke.GetWindowThreadProcessId(shell, &shellProcessId);

        HANDLE shellProcess = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, shellProcessId);
        if (shellProcess.IsNull)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        HANDLE shellToken = default;
        HANDLE token = default;
        try
        {
            if (!PInvoke.OpenProcessToken(shellProcess, TOKEN_ACCESS_MASK.TOKEN_DUPLICATE, &shellToken))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            if (!PInvoke.DuplicateTokenEx(shellToken, PrimaryTokenAccess, null, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                    TOKEN_TYPE.TokenPrimary, &token))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            Start(token, path, CommandLine(path, arguments));
        }
        finally
        {
            CloseIfOpen(token);
            CloseIfOpen(shellToken);
            CloseIfOpen(shellProcess);
        }
    }

    /// <summary>
    ///     The quoted path followed by the arguments, which must be plain switches since nothing quotes them.
    /// </summary>
    public static string CommandLine(string path, ReadOnlySpan<string> arguments)
    {
        if (path.Contains('"'))
        {
            throw new ArgumentException("The path contains a quote.", nameof(path));
        }

        string command = $"\"{path}\"";
        foreach (string argument in arguments)
        {
            if (argument.Length == 0 || argument.Any(character => character == '"' || char.IsWhiteSpace(character)))
            {
                throw new ArgumentException($"The argument '{argument}' would need quoting.", nameof(arguments));
            }

            command += $" {argument}";
        }

        return command;
    }

    private static unsafe void Start(HANDLE token, string path, string commandLine)
    {
        var startup = new STARTUPINFOW { cb = (uint)sizeof(STARTUPINFOW) };

        // CreateProcessWithTokenW may write to the command line, so it gets its own null-terminated copy.
        Span<char> command = [.. commandLine, '\0'];

        // Without an environment block the new process gets the token user's own environment, not this elevated one.
        if (!PInvoke.CreateProcessWithToken(token, 0, path, ref command, 0, null, Path.GetDirectoryName(path), in startup,
                out PROCESS_INFORMATION started))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        PInvoke.CloseHandle(started.hThread);
        PInvoke.CloseHandle(started.hProcess);
    }

    private static void CloseIfOpen(HANDLE handle)
    {
        if (!handle.IsNull)
        {
            PInvoke.CloseHandle(handle);
        }
    }
}
