namespace WslcDesktop.Services;

/// <summary>
///     Reads a release's SHA256SUMS.txt, whose lines are a hash, two spaces and a file name.
/// </summary>
public static class ChecksumFile
{
    private const int Sha256HexLength = 64;

    /// <summary>
    ///     The lowercase SHA-256 listed for the asset, or null when the file has no valid line for it.
    /// </summary>
    public static string? Find(string text, string assetName)
    {
        foreach (string line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = line.IndexOf(' ');
            if (separator != Sha256HexLength)
            {
                continue;
            }

            // sha256sum marks a file read in binary mode with an asterisk before its name.
            string name = line[separator..].TrimStart(' ').TrimStart('*');
            string hash = line[..separator];
            if (name == assetName && hash.All(char.IsAsciiHexDigit))
            {
                return hash.ToLowerInvariant();
            }
        }

        return null;
    }
}
