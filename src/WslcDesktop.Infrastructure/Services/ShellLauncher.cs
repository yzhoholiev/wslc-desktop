using System.ComponentModel;
using System.Diagnostics;

namespace WslcDesktop.Services;

/// <summary>
///     Opens terminals, editors, folders and links outside the app.
/// </summary>
internal sealed class ShellLauncher(Notifier notifier) : IShellLauncher
{
    public void OpenContainerShell(string containerId) =>
        OpenTerminal(WslcCli.ExecutablePath, "exec", "--interactive", "--tty", containerId, "/bin/sh");

    public void OpenDistroShell(string distro) => OpenTerminal(WslCli.ExecutablePath, "--distribution", distro, "--cd", "~");

    public void OpenSessionShell(string session) => OpenTerminal(WslcCli.ExecutablePath, "--session", session, "system", "session", "shell");

    public void OpenWslConfig() => Start("notepad.exe", [WslConfigFile.FilePath]);

    public void OpenWslSettings() => Start(WslCli.SettingsAppPath, []);

    public void OpenUrl(Uri url)
    {
        if (!TryStart(url.AbsoluteUri, [], out string? error))
        {
            notifier.Error($"Could not open {url}", error);
        }
    }

    public void OpenDataFolder() => OpenFolder(Path.GetDirectoryName(CrashLogFile.FilePath)!);

    public void OpenLicenses() => Start("notepad.exe", [Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.txt")]);

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            notifier.Error($"Could not open {path}", ex.Message);
            return;
        }

        Start("explorer.exe", [path]);
    }

    private void OpenTerminal(string fileName, params string[] arguments)
    {
        string[] terminalArguments = ["--", fileName, .. arguments.Select(argument => argument.Replace(";", "\\;", StringComparison.Ordinal))];
        if (!TryStart("wt.exe", terminalArguments, out _) && !TryStart(fileName, arguments, out string? error))
        {
            notifier.Error("Could not open a terminal", error);
        }
    }

    private void Start(string fileName, string[] arguments)
    {
        if (!TryStart(fileName, arguments, out string? error))
        {
            notifier.Error($"Could not start {Path.GetFileName(fileName)}", error);
        }
    }

    private static bool TryStart(string fileName, string[] arguments, out string error)
    {
        var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = true };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            Process.Start(startInfo)?.Dispose();
            error = "";
            return true;
        }
        catch (Win32Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
