namespace WslcDesktop.Services;

/// <summary>
///     The global WSL 2 settings in .wslconfig.
/// </summary>
public interface IWslConfig
{
    /// <summary>
    ///     The file's text, or empty when it is missing or unreadable.
    /// </summary>
    string ReadText();
}
