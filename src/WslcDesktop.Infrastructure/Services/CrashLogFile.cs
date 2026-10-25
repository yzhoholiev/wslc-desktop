namespace WslcDesktop.Services;

/// <summary>
///     Appends failures to %LocalAppData%\WslcDesktop\crash.log.
/// </summary>
public static class CrashLogFile
{
    private const long MaxBytes = 1024 * 1024;

    private static readonly Lock Sync = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WslcDesktop", "crash.log");

    public static void Write(Exception exception)
    {
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                {
                    File.Move(FilePath, Path.ChangeExtension(FilePath, ".old.log"), true);
                }

                File.AppendAllText(FilePath, $"[{DateTimeOffset.Now:O}] {exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
