using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace WslcDesktop.UiTests;

/// <summary>
///     One running instance of the published app, started with default settings and stopped afterwards.
/// </summary>
public sealed partial class AppSession : IAsyncLifetime
{
    private const string ProcessName = "WslcDesktop";
    private const uint WmClose = 0x0010;
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);

    private static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WslcDesktop");

    private static readonly string SettingsFile = Path.Combine(DataDirectory, "settings.json");
    private static readonly string CrashLogFile = Path.Combine(DataDirectory, "crash.log");

    /// <summary>
    ///     Kept on disk until restored, so a run that dies midway still gives the user's settings back on the next run.
    /// </summary>
    private static readonly string SettingsBackupFile = Path.Combine(DataDirectory, "settings.uitests-backup.json");

    private DateTime _crashLogBaseline;

    private bool _started;

    public static bool Enabled => Environment.GetEnvironmentVariable("WSLC_UI_TESTS") == "1";

    public static string Executable { get; } = Environment.GetEnvironmentVariable("WSLC_DESKTOP_EXE") is { Length: > 0 } path
        ? path
        : Path.Combine(FindRepositoryRoot(), "publish", "win-x64", "WslcDesktop.exe");

    public Process? Process { get; private set; }

    public AutomationElement Window => AutomationElement.FromHandle(MainWindowHandle());

    public async ValueTask InitializeAsync()
    {
        if (!Enabled && !RuntimeFixture.Enabled)
        {
            return;
        }

        // The app is single-instance, so a running copy would receive these clicks; never close the user's app.
        if (Process.GetProcessesByName(ProcessName).Length > 0)
        {
            throw new InvalidOperationException("Close WSLC Desktop before running the UI tests.");
        }

        if (!File.Exists(Executable))
        {
            throw new FileNotFoundException("Publish the app first (publish.ps1) or set WSLC_DESKTOP_EXE.", Executable);
        }

        if (!File.Exists(SettingsBackupFile) && File.Exists(SettingsFile))
        {
            File.Copy(SettingsFile, SettingsBackupFile);
        }

        _started = true;
        File.Delete(SettingsFile);
        _crashLogBaseline = File.Exists(CrashLogFile) ? File.GetLastWriteTimeUtc(CrashLogFile) : DateTime.MinValue;

        await LaunchAsync();
        Process = await WaitForAsync(() => Process.GetProcessesByName(ProcessName).FirstOrDefault(process => process.MainWindowHandle != 0),
                StartTimeout)
            ?? throw new TimeoutException("The app window did not appear.");

        await WaitForAsync(() => Find(Window, "Dashboard", ControlType.ListItem), StartTimeout);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_started)
        {
            return;
        }

        foreach (Process process in Process.GetProcessesByName(ProcessName))
        {
            process.Kill();
            await process.WaitForExitAsync();
        }

        if (File.Exists(SettingsBackupFile))
        {
            File.Move(SettingsBackupFile, SettingsFile, true);
        }
        else
        {
            File.Delete(SettingsFile);
        }
    }

    /// <summary>
    ///     Starts the published exe; while an instance runs, that instance comes to the front instead.
    /// </summary>
    public static async Task LaunchAsync()
    {
        using var launcher = Process.Start(Executable);
        await launcher.WaitForExitAsync();
    }

    public bool CrashLogChanged() => File.Exists(CrashLogFile) && File.GetLastWriteTimeUtc(CrashLogFile) > _crashLogBaseline;

    public string CrashLogTail() => File.Exists(CrashLogFile) ? string.Join(Environment.NewLine, File.ReadLines(CrashLogFile).TakeLast(20)) : "";

    public void CloseWindow() => SendMessage(MainWindowHandle(), WmClose, 0, 0);

    public bool IsWindowVisible() => IsWindowVisible(MainWindowHandle());

    public void Navigate(string page)
    {
        AutomationElement item = Find(Window, page, ControlType.ListItem) ?? throw new InvalidOperationException($"No navigation item '{page}'.");
        ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
    }

    public static void Invoke(AutomationElement element) => ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();

    public static AutomationElement? Find(AutomationElement scope, string name, ControlType? type = null)
    {
        Condition condition = new PropertyCondition(AutomationElement.NameProperty, name);
        if (type is not null)
        {
            condition = new AndCondition(condition, new PropertyCondition(AutomationElement.ControlTypeProperty, type));
        }

        return scope.FindFirst(TreeScope.Descendants, condition);
    }

    public static async Task<T?> WaitForAsync<T>(Func<T?> probe, TimeSpan? timeout = null)
    {
        DateTime deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (true)
        {
            try
            {
                if (probe() is { } value && (value is not bool flag || flag))
                {
                    return value;
                }
            }
            catch (ElementNotAvailableException)
            {
            }

            if (DateTime.UtcNow > deadline)
            {
                return default;
            }

            await Task.Delay(250);
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WslcDesktop.slnx")))
            {
                return directory.FullName;
            }
        }

        return AppContext.BaseDirectory;
    }

    private nint MainWindowHandle()
    {
        Process process = Process ?? throw new InvalidOperationException("The app is not running.");
        process.Refresh();
        return process.MainWindowHandle;
    }

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial nint SendMessage(nint hWnd, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "IsWindowVisible")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint hWnd);
}
