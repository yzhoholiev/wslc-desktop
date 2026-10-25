namespace WslcDesktop.Services;

/// <summary>
///     Reads settings files that may be missing or locked, treating both as empty.
/// </summary>
internal static class TextFile
{
    public static string ReadOrEmpty(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }
}
