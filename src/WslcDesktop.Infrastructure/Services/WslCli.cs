using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Typed access to wsl.exe for distribution and platform management.
/// </summary>
internal sealed class WslCli : IWslPlatform
{
    /// <summary>
    ///     wsl.exe writes UTF-16 to redirected streams unless told otherwise.
    /// </summary>
    private static readonly Dictionary<string, string> Utf8Output = new() { ["WSL_UTF8"] = "1" };

    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    ///     An update downloads the WSL package.
    /// </summary>
    private static readonly TimeSpan UpdateTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    ///     The list of distributions to install is downloaded.
    /// </summary>
    private static readonly TimeSpan OnlineListTimeout = TimeSpan.FromMinutes(1);

    public static string ExecutablePath { get; } = Path.Combine(Environment.SystemDirectory, "wsl.exe");

    public static string SettingsAppPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WSL", "wslsettings", "wslsettings.exe");

    public bool IsSettingsAppInstalled => File.Exists(SettingsAppPath);

    public async Task<List<WslDistro>> ListDistrosAsync(CancellationToken ct = default)
    {
        CliResult result = await Cli.RunAsync(ExecutablePath, ["--list", "--verbose"], Utf8Output, QueryTimeout, ct);
        return result.Succeeded ? ParseDistros(result.StdOut) : [];
    }

    public async Task<List<KeyValueEntry>> GetVersionInfoAsync(CancellationToken ct = default)
    {
        string output = await Cli.RunCheckedAsync(ExecutablePath, ["--version"], Utf8Output, QueryTimeout, ct);
        return ParseKeyValues(output);
    }

    public Task ShutdownAsync() => RunAsync(OperationTimeout, "--shutdown");

    public Task TerminateAsync(string distro) => RunAsync(OperationTimeout, "--terminate", distro);

    public Task SetDefaultAsync(string distro) => RunAsync(QueryTimeout, "--set-default", distro);

    public Task UnregisterAsync(string distro) => RunAsync(OperationTimeout, "--unregister", distro);

    public async Task<List<AvailableDistro>> ListAvailableDistrosAsync(CancellationToken ct = default)
    {
        string output = await Cli.RunCheckedAsync(ExecutablePath, ["--list", "--online"], Utf8Output, OnlineListTimeout, ct);
        return ParseAvailableDistros(output);
    }

    public Task<string> UpdateAsync(bool preRelease) =>
        preRelease ? RunAsync(UpdateTimeout, "--update", "--pre-release") : RunAsync(UpdateTimeout, "--update");

    internal static List<WslDistro> ParseDistros(string output)
    {
        List<string> lines = Lines(output);
        if (lines.Count == 0 || ColumnStarts(lines[0]) is not [var nameStart, var stateStart, var versionStart])
        {
            return [];
        }

        var distros = new List<WslDistro>();
        foreach (string line in lines.Skip(1).Where(line => line.Length > versionStart))
        {
            string name = line[nameStart..stateStart].Trim();
            if (name.Length > 0)
            {
                distros.Add(new WslDistro(
                    name,
                    line[stateStart..versionStart].Trim(),
                    line[versionStart..].Trim(),
                    line.TrimStart().StartsWith('*')));
            }
        }

        return distros;
    }

    internal static List<AvailableDistro> ParseAvailableDistros(string output)
    {
        // wsl.exe translates the column headers, so the table is found as the block after the introduction.
        List<string> lines = [.. Lines(output).SkipWhile(line => line.Length == 0)];
        int introductionEnd = lines.IndexOf("");
        string[] table = [.. lines.Skip(introductionEnd + 1).TakeWhile(line => line.Length > 0)];
        if (table is not [var header, .. var rows] || ColumnStarts(header) is not [var nameStart, var friendlyStart, ..])
        {
            return [];
        }

        var distros = new List<AvailableDistro>();
        foreach (string row in rows.Where(row => row.Length > nameStart))
        {
            string name = row[nameStart..Math.Min(friendlyStart, row.Length)].Trim();
            string friendlyName = row.Length > friendlyStart ? row[friendlyStart..].Trim() : "";
            if (name.Length > 0)
            {
                distros.Add(new AvailableDistro(name, friendlyName.Length > 0 ? friendlyName : name));
            }
        }

        return distros;
    }

    /// <summary>
    ///     wsl.exe may pad its output with NULs and ends lines with CRLF.
    /// </summary>
    private static List<string> Lines(string output) =>
        [.. output.Replace("\0", "", StringComparison.Ordinal).Split('\n').Select(line => line.TrimEnd('\r', ' '))];

    /// <summary>
    ///     Names may contain spaces, so columns are read where the header's words start.
    /// </summary>
    private static int[] ColumnStarts(string header) =>
    [
        .. header.Select((character, index) => (character, index))
            .Where(item => item.character != ' ' && (item.index == 0 || header[item.index - 1] == ' '))
            .Select(item => item.index)
    ];

    private static Task<string> RunAsync(TimeSpan timeout, params string[] arguments) =>
        Cli.RunCheckedAsync(ExecutablePath, arguments, Utf8Output, timeout);

    internal static List<KeyValueEntry> ParseKeyValues(string output) =>
    [
        .. output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts is [_, { Length: > 0 }])
            .Select(parts => new KeyValueEntry(parts[0], parts[1]))
    ];
}
