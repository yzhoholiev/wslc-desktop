namespace WslcDesktop.Services;

/// <summary>
///     Reads wslc's settings from %LocalAppData%\wslc\settings.yaml, which exists only once `wslc settings` has created it.
/// </summary>
internal sealed class WslcSettingsFile : IWslcSettings
{
    public string FilePath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "wslc", "settings.yaml");

    public string ReadText() => TextFile.ReadOrEmpty(FilePath);
}
