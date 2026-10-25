using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WslcDesktop.Launcher;

/// <summary>
///     Single-file distribution of the WinUI app: unpacks the embedded app once per version and starts it.
/// </summary>
internal static class Program
{
    private const string PayloadResource = "payload.zip";
    private const string PayloadVersionMetadata = "PayloadVersion";
    private const string AppExecutable = "WslcDesktop.exe";
    private const string CompleteMarker = ".complete";
    private const string TemporarySuffix = ".tmp";

    /// <summary>
    ///     Serializes unpacking and cleanup across concurrent launches, e.g. sign-in autostart and a double-click.
    /// </summary>
    private const string UnpackMutexName = @"Local\WslcDesktop.Launcher.Unpack";

    /// <summary>
    ///     Read by the app so that "Start with Windows" registers this launcher rather than an unpacked, versioned copy.
    /// </summary>
    private const string LauncherPathVariable = "WSLCDESKTOP_LAUNCHER";

    /// <summary>
    ///     Antivirus scanners briefly lock freshly unpacked files, which fails the move into place.
    /// </summary>
    private const int MoveAttempts = 5;

    private static readonly TimeSpan MoveRetryDelay = TimeSpan.FromMilliseconds(250);

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string versionsRoot = VersionsRoot();
            using var unpackLock = new Mutex(false, UnpackMutexName);
            AcquireOrRecover(unpackLock);
            try
            {
                string appDirectory = EnsureUnpacked(versionsRoot);
                Start(appDirectory, args);
                RemoveStaleVersions(versionsRoot, appDirectory);
            }
            finally
            {
                unpackLock.ReleaseMutex();
            }

            return 0;
        }
        catch (Exception ex)
        {
            // A windowed process has no console, so anything unreported here would end the launch without a word.
            PInvoke.MessageBox(default, $"WSLC Desktop could not start.\n\n{ex.Message}", "WSLC Desktop", MESSAGEBOX_STYLE.MB_ICONERROR);
            return 1;
        }
    }

    private static string VersionsRoot()
    {
        // GetFolderPath returns "" when the folder is missing, which would unpack relative to the working directory.
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.IsPathFullyQualified(localAppData)
            ? Path.Combine(localAppData, "WslcDesktop", "app")
            : throw new IOException("The local application data folder (%LOCALAPPDATA%) is not available.");
    }

    private static void AcquireOrRecover(Mutex mutex)
    {
        try
        {
            mutex.WaitOne();
        }
        catch (AbandonedMutexException)
        {
            // A launcher died holding the lock; this one now owns it, and the dead launch's folder lacks a completion marker.
        }
    }

    private static string EnsureUnpacked(string versionsRoot)
    {
        using Stream payload = typeof(Program).Assembly.GetManifestResourceStream(PayloadResource)
            ?? throw new InvalidDataException("This build does not contain the application payload.");

        string target = Path.Combine(versionsRoot, PayloadVersion(payload));
        if (File.Exists(Path.Combine(target, CompleteMarker)))
        {
            return target;
        }

        // Unpack beside the target and move it into place, so a crash never leaves a half-written version that looks complete.
        string staging = $"{target}.{Guid.NewGuid():N}{TemporarySuffix}";
        payload.Position = 0;
        ZipFile.ExtractToDirectory(payload, staging);
        File.WriteAllText(Path.Combine(staging, CompleteMarker), "");

        // Under the unpack lock, a target without a completion marker can only be the remains of a crashed launch.
        if (Directory.Exists(target))
        {
            Directory.Delete(target, true);
        }

        MoveIntoPlace(staging, target);
        return target;
    }

    private static void MoveIntoPlace(string staging, string target)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Move(staging, target);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < MoveAttempts)
            {
                Thread.Sleep(MoveRetryDelay * attempt);
            }
        }
    }

    /// <summary>
    ///     Name of the folder this build's payload unpacks into.
    /// </summary>
    private static string PayloadVersion(Stream payload)
    {
        string? stamped = typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == PayloadVersionMetadata)?.Value;

        return string.IsNullOrEmpty(stamped)
            ? Convert.ToHexString(SHA256.HashData(payload))[..16].ToLowerInvariant()
            : stamped;
    }

    private static void Start(string appDirectory, string[] args)
    {
        var startInfo = new ProcessStartInfo(Path.Combine(appDirectory, AppExecutable))
        {
            UseShellExecute = false,
            WorkingDirectory = appDirectory
        };

        foreach (string argument in args)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment[LauncherPathVariable] = Environment.ProcessPath;
        Process.Start(startInfo)?.Dispose();
    }

    private static void RemoveStaleVersions(string versionsRoot, string currentDirectory)
    {
        foreach (string directory in Directory.EnumerateDirectories(versionsRoot))
        {
            bool isCurrent = string.Equals(directory, currentDirectory, StringComparison.OrdinalIgnoreCase);

            // Under the unpack lock no other launch is writing, so any staging folder is abandoned.
            bool isStaging = directory.EndsWith(TemporarySuffix, StringComparison.Ordinal);

            if (!isCurrent && (isStaging || !IsRunning(Path.Combine(directory, AppExecutable))))
            {
                TryDelete(directory);
            }
        }
    }

    /// <summary>
    ///     A running executable is mapped by the loader and cannot be opened for exclusive access.
    /// </summary>
    private static bool IsRunning(string executable)
    {
        if (!File.Exists(executable))
        {
            return false;
        }

        try
        {
            using FileStream exclusive = File.Open(executable, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static void TryDelete(string directory)
    {
        try
        {
            // Without its marker a version that is only partly deleted is unpacked again instead of started.
            File.Delete(Path.Combine(directory, CompleteMarker));
            Directory.Delete(directory, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
