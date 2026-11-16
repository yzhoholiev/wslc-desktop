namespace WslcDesktop.Services;

/// <summary>
///     Checks distribution names before they reach wsl.exe as an install target.
/// </summary>
public static class DistroName
{
    /// <summary>
    ///     Whether the text can name a distribution to install: what WSL's own names use, and no leading '-' that wsl.exe would read as an option.
    /// </summary>
    public static bool IsValid(string name) =>
        name.Length > 0
        && name[0] != '-'
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
}
