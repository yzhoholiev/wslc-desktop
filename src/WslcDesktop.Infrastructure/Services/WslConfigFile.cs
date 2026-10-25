namespace WslcDesktop.Services;

/// <summary>
///     Reads the global WSL 2 settings from %UserProfile%\.wslconfig.
/// </summary>
internal sealed class WslConfigFile : IWslConfig
{
    public static string FilePath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wslconfig");

    public string ReadText() => TextFile.ReadOrEmpty(FilePath);
}
