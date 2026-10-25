namespace WslcDesktop.Services;

/// <summary>
///     wslc's per-user settings.yaml, which configures the session VM wslc starts.
/// </summary>
public interface IWslcSettings
{
    string FilePath { get; }

    /// <summary>
    ///     The file's text, or empty when it is missing or unreadable.
    /// </summary>
    string ReadText();
}
