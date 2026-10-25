using System.Collections.Frozen;
using System.Globalization;

namespace WslcDesktop.Services;

/// <summary>
///     Parses the sizes and percentages wslc prints, and formats sizes with IEC units.
/// </summary>
public static class ByteSize
{
    private static readonly string[] Units = ["B", "KiB", "MiB", "GiB", "TiB"];

    /// <summary>
    ///     Bytes per unit wslc prints, decimal (kB, MB) as well as binary (KiB, MiB).
    /// </summary>
    private static readonly FrozenDictionary<string, double> BytesPerUnit = FrozenDictionary.Create(
        StringComparer.OrdinalIgnoreCase,
        KeyValuePair.Create("", 1d),
        KeyValuePair.Create("B", 1d),
        KeyValuePair.Create("KB", 1e3),
        KeyValuePair.Create("MB", 1e6),
        KeyValuePair.Create("GB", 1e9),
        KeyValuePair.Create("TB", 1e12),
        KeyValuePair.Create("KIB", 1024d),
        KeyValuePair.Create("MIB", 1024d * 1024),
        KeyValuePair.Create("GIB", 1024d * 1024 * 1024),
        KeyValuePair.Create("TIB", 1024d * 1024 * 1024 * 1024));

    /// <summary>
    ///     Parses sizes printed by wslc, such as "120MB", "15.1MiB" or "0B".
    /// </summary>
    public static long Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        string trimmed = text.Trim();
        int unitStart = trimmed.AsSpan().IndexOfAnyExcept("0123456789.") is >= 0 and var index ? index : trimmed.Length;

        if (!double.TryParse(trimmed[..unitStart], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            return 0;
        }

        double multiplier = BytesPerUnit.GetValueOrDefault(trimmed[unitStart..].Trim());

        return (long)(value * multiplier);
    }

    /// <summary>
    ///     Formats memory and disk sizes with binary (IEC) units.
    /// </summary>
    public static string Format(long bytes)
    {
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : string.Create(CultureInfo.InvariantCulture, $"{value:0.#} {Units[unit]}");
    }

    /// <summary>
    ///     Rewrites a size wslc printed, in whatever units, with IEC units.
    /// </summary>
    public static string Normalize(string text) => string.IsNullOrWhiteSpace(text) ? text : Format(Parse(text));

    public static double ParsePercent(string? text) =>
        double.TryParse(text?.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;
}
