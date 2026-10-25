using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WslcDesktop.Formatting;

public enum LogTone
{
    Default,
    Timestamp,
    Error,
    Warning,
    Dim
}

/// <summary>
///     One styled piece of a log line; <see cref="Ansi" /> is a basic color (0–15) and <see cref="Rgb" /> a 0xRRGGBB color, at most one set.
/// </summary>
public readonly record struct LogSegment(string Text, LogTone Tone = LogTone.Default, int? Ansi = null, int? Rgb = null, bool IsBold = false);

/// <summary>
///     Splits container log lines into styled pieces.
/// </summary>
public static partial class LogParser
{
    private const int MaxStackallocCodes = 32;

    private static readonly int[] CubeLevels = [0, 95, 135, 175, 215, 255];

    /// <summary>
    ///     The log without escape sequences, and without the timestamp of each line unless <paramref name="showTimestamps" /> is set.
    /// </summary>
    public static string ToPlainText(string log, bool showTimestamps)
    {
        var plain = new StringBuilder(log.Length);
        ReadOnlySpan<char> text = log;
        foreach (Range range in text.Split('\n'))
        {
            if (range.Start.Value > 0)
            {
                plain.Append('\n');
            }

            ReadOnlySpan<char> line = text[range].TrimEnd('\r');
            int start = TimestampLength(line);
            if (showTimestamps)
            {
                plain.Append(line[..start]);
            }

            ReadOnlySpan<char> content = line[start..];
            plain.Append(content[VisibleFrom(content)..]);
        }

        return EscapePattern().Replace(plain.ToString(), "");
    }

    /// <summary>
    ///     Adds the styled pieces of one log line to <paramref name="segments" />.
    /// </summary>
    public static void ParseLine(ReadOnlySpan<char> line, bool showTimestamp, List<LogSegment> segments)
    {
        int start = TimestampLength(line);
        if (start > 0 && showTimestamp)
        {
            segments.Add(new LogSegment(line[..start].ToString(), LogTone.Timestamp));
        }

        ReadOnlySpan<char> text = line[start..].TrimEnd('\r');
        int visibleFrom = VisibleFrom(text);
        if (!text.Contains('\x1b'))
        {
            text = text[visibleFrom..];
            if (!text.IsEmpty)
            {
                segments.Add(new LogSegment(text.ToString(), ToneOf(text)));
            }

            return;
        }

        var style = new Style();
        int position = 0;
        foreach (ValueMatch escape in EscapePattern().EnumerateMatches(text))
        {
            AddVisible(segments, style, text[..escape.Index], position, visibleFrom);
            position = escape.Index + escape.Length;
            if (text[position - 1] == 'm')
            {
                style = Apply(style, text.Slice(escape.Index + 2, escape.Length - 3));
            }
        }

        AddVisible(segments, style, text, position, visibleFrom);
    }

    /// <summary>
    ///     An RFC 3339 timestamp and its separating space, as `wslc logs --timestamps` prefixes each line.
    /// </summary>
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2}) ")]
    private static partial Regex TimestampPattern();

    [GeneratedRegex(@"\b(?:error|fatal|panic|exception|critical|crit|emerg|alert)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ErrorPattern();

    [GeneratedRegex(@"\b(?:warn|warning)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WarningPattern();

    /// <summary>
    ///     An escape sequence: ESC [ parameters final-letter, e.g. "\e[1;31m" (SGR) or "\e[K" (erase line).
    /// </summary>
    [GeneratedRegex(@"\x1b\[([0-9;]*)([A-Za-z])")]
    private static partial Regex EscapePattern();

    private static int TimestampLength(ReadOnlySpan<char> line)
    {
        Regex.ValueMatchEnumerator matches = TimestampPattern().EnumerateMatches(line);
        return matches.MoveNext() ? matches.Current.Length : 0;
    }

    /// <summary>
    ///     Where the text a terminal shows starts: what follows the last carriage return overwrites the line, as in progress output.
    /// </summary>
    private static int VisibleFrom(ReadOnlySpan<char> text) => text.LastIndexOf('\r') + 1;

    /// <summary>
    ///     Adds the part of <paramref name="text" /> from <paramref name="start" /> that is not overwritten after a carriage return.
    /// </summary>
    private static void AddVisible(List<LogSegment> segments, Style style, ReadOnlySpan<char> text, int start, int visibleFrom)
    {
        start = Math.Max(start, visibleFrom);
        if (start < text.Length)
        {
            segments.Add(style.Segment(text[start..]));
        }
    }

    private static LogTone ToneOf(ReadOnlySpan<char> text) =>
        ErrorPattern().IsMatch(text) ? LogTone.Error : WarningPattern().IsMatch(text) ? LogTone.Warning : LogTone.Default;

    /// <summary>
    ///     Applies an SGR parameter list, such as "1;31" or "38;5;208".
    /// </summary>
    private static Style Apply(Style style, ReadOnlySpan<char> parameters)
    {
        // An empty list splits into one empty code, read as 0: a reset, as terminals treat "\e[m".
        int count = parameters.Count(';') + 1;
        Span<int> codes = count <= MaxStackallocCodes ? stackalloc int[count] : new int[count];
        int next = 0;
        foreach (Range code in parameters.Split(';'))
        {
            codes[next++] = int.TryParse(parameters[code], CultureInfo.InvariantCulture, out int value) ? value : 0;
        }

        for (int i = 0; i < codes.Length; i++)
        {
            style = codes[i] switch
            {
                0 => new Style(),
                1 => style with { IsBold = true },
                2 => style with { IsDim = true },
                22 => style with { IsBold = false, IsDim = false },
                >= 30 and <= 37 => style with { Ansi = codes[i] - 30, Rgb = null },
                >= 90 and <= 97 => style with { Ansi = codes[i] - 90 + 8, Rgb = null },
                39 => style with { Ansi = null, Rgb = null },
                38 when i + 2 < codes.Length && codes[i + 1] == 5 => Color256(style, codes[i + 2]),
                38 when i + 4 < codes.Length && codes[i + 1] == 2 => style with { Ansi = null, Rgb = ToRgb(codes[i + 2], codes[i + 3], codes[i + 4]) },
                _ => style
            };

            // Extended colors carry their own arguments, foreground (38) and background (48) alike.
            if (codes[i] is 38 or 48 && i + 1 < codes.Length)
            {
                i += codes[i + 1] == 5 ? 2 : codes[i + 1] == 2 ? 4 : 0;
            }
        }

        return style;
    }

    /// <summary>
    ///     xterm's 256 colors: the 16 basic ones, a 6×6×6 color cube, then 24 grays.
    /// </summary>
    private static Style Color256(Style style, int index)
    {
        switch (index)
        {
            case < 16:
                return style with { Ansi = Math.Max(0, index), Rgb = null };
            case < 232:
            {
                int cube = index - 16;
                return style with { Ansi = null, Rgb = ToRgb(CubeLevels[cube / 36], CubeLevels[cube / 6 % 6], CubeLevels[cube % 6]) };
            }
            default:
            {
                int gray = 8 + 10 * (Math.Min(index, 255) - 232);
                return style with { Ansi = null, Rgb = ToRgb(gray, gray, gray) };
            }
        }
    }

    private static int ToRgb(int r, int g, int b) => (Math.Clamp(r, 0, 255) << 16) | (Math.Clamp(g, 0, 255) << 8) | Math.Clamp(b, 0, 255);

    private readonly record struct Style(int? Ansi = null, int? Rgb = null, bool IsBold = false, bool IsDim = false)
    {
        public LogSegment Segment(ReadOnlySpan<char> text) =>
            new(text.ToString(), IsDim && Ansi is null && Rgb is null ? LogTone.Dim : LogTone.Default, Ansi, Rgb, IsBold);
    }
}
