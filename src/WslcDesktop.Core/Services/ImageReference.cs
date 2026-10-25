namespace WslcDesktop.Services;

/// <summary>
///     Checks image names typed or read before they reach wslc or a shell command line.
/// </summary>
public static class ImageReference
{
    /// <summary>
    ///     Whether the text can be an image reference or ID: what references allow, and no leading '-' that wslc would read as an option.
    /// </summary>
    public static bool IsValid(string reference) =>
        reference.Length > 0
        && char.IsAsciiLetterOrDigit(reference[0])
        && reference.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or ':' or '/' or '@');
}
