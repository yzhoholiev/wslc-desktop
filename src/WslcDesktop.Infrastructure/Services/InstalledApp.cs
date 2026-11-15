using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace WslcDesktop.Services;

/// <summary>
///     The running app and the launcher file it was started from, which an update replaces.
/// </summary>
internal sealed class InstalledApp(string? launcherPath, AppVersion runningVersion, Architecture architecture) : IInstalledApp
{
    private const string DownloadSuffix = ".download";
    private const string BackupSuffix = ".old";

    /// <summary>
    ///     Held by the launcher while it unpacks and starts the app, so a swap never lands in the middle of a launch.
    /// </summary>
    private const string UnpackMutexName = @"Local\WslcDesktop.Launcher.Unpack";

    /// <summary>
    ///     Antivirus scanners and a starting launcher briefly lock the file, which fails the swap.
    /// </summary>
    private const int SwapAttempts = 5;

    private static readonly TimeSpan SwapRetryDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan UnpackLockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LeftoverAge = TimeSpan.FromDays(1);

    public AppVersion RunningVersion => runningVersion;

    public string AssetName => $"WslcDesktop-win-{(architecture == Architecture.Arm64 ? "arm64" : "x64")}.exe";

    public bool HasLauncher => launcherPath is not null;

    public AppVersion? ReadLauncherVersion()
    {
        if (launcherPath is null)
        {
            return null;
        }

        try
        {
            return AppVersion.TryParse(FileVersionInfo.GetVersionInfo(launcherPath).ProductVersion, out AppVersion? version) ? version : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public string ReserveDownloadPath()
    {
        string launcher = RequireLauncher();
        string folder = Path.GetDirectoryName(launcher)!;
        string path = $"{launcher}.{Guid.NewGuid():N}{DownloadSuffix}";
        try
        {
            // The download is written by another call, so the folder is probed here, where a refusal can still stop it.
            using (new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }

            RemoveLeftovers(launcher, folder);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UpdateException($"WSLC Desktop cannot write to {folder}.", ex);
        }
    }

    public async Task<(long Size, string Sha256)> InspectAsync(string path, CancellationToken ct)
    {
        try
        {
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            byte[] hash = await SHA256.HashDataAsync(file, ct);
            return (file.Length, Convert.ToHexStringLower(hash));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UpdateException("The download could not be read.", ex);
        }
    }

    public void Install(string downloadedPath)
    {
        string launcher = RequireLauncher();
        if (!File.Exists(downloadedPath) || !File.Exists(launcher))
        {
            throw new UpdateException("WSLC Desktop could not be replaced: a file is missing.");
        }

        string backup = launcher + BackupSuffix;
        using var unpackLock = new UnpackLock();
        try
        {
            Swap(downloadedPath, launcher, backup);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // File.Replace leaves the launcher in place when it fails; this covers the case where it did not.
            if (!File.Exists(launcher) && File.Exists(backup))
            {
                Restore(backup, launcher);
            }

            throw new UpdateException($"WSLC Desktop could not be replaced: {ex.Message}", ex);
        }

        Discard(backup);
    }

    public void Discard(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void Restart(string[] arguments)
    {
        var startInfo = new ProcessStartInfo(RequireLauncher()) { UseShellExecute = false };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process.Start(startInfo)?.Dispose();
    }

    /// <summary>
    ///     The version of this process, as its entry assembly states it.
    /// </summary>
    public static AppVersion ReadRunningVersion()
    {
        string? text = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (AppVersion.TryParse(text, out AppVersion? version))
        {
            return version;
        }

        // An unreadable version sorts below every release, so the app still offers updates.
        AppVersion.TryParse("0.0.0", out AppVersion? unknown);
        return unknown!;
    }

    private string RequireLauncher() => launcherPath ?? throw new UpdateException("WSLC Desktop was not started from its launcher.");

    private static void Restore(string backup, string launcher)
    {
        try
        {
            File.Move(backup, launcher);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UpdateException($"WSLC Desktop could not be replaced, and the previous version is now at {backup}. Rename it back to start the app.", ex);
        }
    }

    private static void Swap(string downloadedPath, string launcher, string backup)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Replace(downloadedPath, launcher, backup, true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < SwapAttempts)
            {
                Thread.Sleep(SwapRetryDelay);
            }
        }
    }

    private void RemoveLeftovers(string launcher, string folder)
    {
        DateTime cutoff = DateTime.UtcNow - LeftoverAge;
        string name = Path.GetFileName(launcher);
        IEnumerable<string> leftovers = Directory.EnumerateFiles(folder, $"{name}.*{DownloadSuffix}").Append(launcher + BackupSuffix);
        foreach (string file in leftovers.Where(file => File.Exists(file) && File.GetLastWriteTimeUtc(file) < cutoff))
        {
            Discard(file);
        }
    }

    /// <summary>
    ///     The launcher's unpack lock, taken when it can be: the other elevation level may own it with rights this process lacks.
    /// </summary>
    private sealed class UnpackLock : IDisposable
    {
        private readonly bool _isHeld;
        private readonly Mutex? _mutex;

        public UnpackLock()
        {
            try
            {
                _mutex = new Mutex(false, UnpackMutexName);
                _isHeld = _mutex.WaitOne(UnpackLockTimeout);
            }
            catch (AbandonedMutexException)
            {
                _isHeld = true;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or WaitHandleCannotBeOpenedException or IOException)
            {
            }
        }

        public void Dispose()
        {
            if (_isHeld)
            {
                _mutex?.ReleaseMutex();
            }

            _mutex?.Dispose();
        }
    }
}
