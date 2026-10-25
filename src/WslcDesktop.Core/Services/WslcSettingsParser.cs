using System.Globalization;
using System.Text.RegularExpressions;
using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Reads wslc's settings.yaml as wslc does and describes the session settings it leaves in effect.
/// </summary>
public static partial class WslcSettingsParser
{
    /// <summary>
    ///     wslc rejects smaller sizes and uses the default instead.
    /// </summary>
    private const long MinimumSize = 1L << 20;

    private const long DefaultMaxStorageSize = 1L << 40;

    public static WslcSettings Parse(string yaml)
    {
        Dictionary<string, string> values = ReadValues(yaml);
        return new WslcSettings(
            Positive(values.GetValueOrDefault("session.cpuCount")),
            Size(values.GetValueOrDefault("session.memorySize")),
            Size(values.GetValueOrDefault("session.maxStorageSize")),
            values.GetValueOrDefault("session.storagePath"),
            values.GetValueOrDefault("session.defaultBindingAddress"),
            values.GetValueOrDefault("session.hostLoopback"),
            Positive(values.GetValueOrDefault("session.idleTimeout")),
            values.GetValueOrDefault("credentialStore") is "wincred" or "file" ? values["credentialStore"] : null);
    }

    /// <summary>
    ///     One row per session setting, with the default this host gets where the file sets none.
    /// </summary>
    public static KeyValueEntry[] Describe(WslcSettings settings, int hostProcessors, long hostMemory) =>
    [
        new("CPUs", settings.CpuCount?.ToString(CultureInfo.InvariantCulture) ?? $"{hostProcessors} (default: all logical processors)"),
        new("Memory", settings.MemorySize is { } memory
            ? ByteSize.Format(memory)
            : $"{ByteSize.Format(hostMemory / 2)} (default: half of {ByteSize.Format(hostMemory)})"),
        new("Disk size limit", settings.MaxStorageSize is { } storage ? ByteSize.Format(storage) : $"{ByteSize.Format(DefaultMaxStorageSize)} (default)"),
        new("Storage location", settings.StoragePath ?? "%LOCALAPPDATA% (default)"),
        new("Port binding address", settings.DefaultBindingAddress ?? "127.0.0.1 (default)"),
        new("Windows host name", settings.HostLoopback ?? "host.wslc.internal (default)"),
        new("Idle shutdown", settings.IdleTimeoutSeconds is { } seconds ? $"{seconds} s" : "30 s (default)"),
        new("Registry credentials", settings.CredentialStore switch
        {
            "file" => "Encrypted file",
            "wincred" => "Windows Credential Manager",
            _ => "Windows Credential Manager (default)"
        })
    ];

    /// <summary>
    ///     The scalar values by dotted key, for the two levels settings.yaml uses.
    /// </summary>
    private static Dictionary<string, string> ReadValues(string yaml)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string? section = null;
        foreach (string rawLine in yaml.Split('\n'))
        {
            string line = StripComment(rawLine.TrimEnd('\r'));
            if (line.Trim().Length == 0)
            {
                continue;
            }

            bool indented = char.IsWhiteSpace(line[0]);
            if (line.Split(':', 2) is not [var rawKey, var rawValue])
            {
                continue;
            }

            string key = rawKey.Trim();
            string value = Unquote(rawValue.Trim());
            if (!indented)
            {
                section = value.Length == 0 ? key : null;
            }

            if (value.Length == 0 || value == "default" || (indented && section is null))
            {
                continue;
            }

            values[indented ? $"{section}.{key}" : key] = value;
        }

        return values;
    }

    /// <summary>
    ///     Removes a `#` comment, which starts the line or follows whitespace outside quotes.
    /// </summary>
    private static string StripComment(string line)
    {
        char? quote = null;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quote is not null)
            {
                quote = c == quote ? null : quote;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '#' && (i == 0 || char.IsWhiteSpace(line[i - 1])))
            {
                return line[..i];
            }
        }

        return line;
    }

    private static string Unquote(string value) =>
        value switch
        {
            ['"', .., '"'] => value[1..^1].Replace(@"\\", @"\", StringComparison.Ordinal).Replace("\\\"", "\"", StringComparison.Ordinal),
            ['\'', .., '\''] => value[1..^1].Replace("''", "'", StringComparison.Ordinal),
            _ => value
        };

    private static int? Positive(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number > 0 ? number : null;

    private static long? Size(string? value)
    {
        if (value is null || SizePattern().Match(value) is not { Success: true } match
            || !long.TryParse(match.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out long number))
        {
            return null;
        }

        int shift = char.ToUpperInvariant(match.Groups[2].Value is [var unit, ..] ? unit : 'B') switch
        {
            'K' => 10,
            'M' => 20,
            'G' => 30,
            'T' => 40,
            'P' => 50,
            _ => 0
        };

        long bytes = number << shift;
        return bytes >> shift == number && bytes >= MinimumSize ? bytes : null;
    }

    /// <summary>
    ///     An integer and an optional binary unit: B, K or KB, M or MB, G or GB, T or TB, P or PB, in any case.
    /// </summary>
    [GeneratedRegex("^([0-9]+)\\s*([KMGTP]B?|B)?$", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex SizePattern();
}
