using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace WslcDesktop.Services;

/// <summary>
///     A SemVer 2.0 version of the app, without build metadata.
/// </summary>
public sealed record AppVersion : IComparable<AppVersion>
{
    private AppVersion(int major, int minor, int patch, string preRelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    /// <summary>
    ///     The dot-separated pre-release identifiers, or an empty string for a release.
    /// </summary>
    public string PreRelease { get; }

    public bool IsPreRelease => PreRelease.Length > 0;

    public int CompareTo(AppVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        int core = Major != other.Major ? Major.CompareTo(other.Major)
            : Minor != other.Minor ? Minor.CompareTo(other.Minor)
            : Patch.CompareTo(other.Patch);

        if (core != 0 || IsPreRelease == other.IsPreRelease && !IsPreRelease)
        {
            return core;
        }

        // A release is higher than any of its pre-releases.
        if (IsPreRelease != other.IsPreRelease)
        {
            return IsPreRelease ? -1 : 1;
        }

        return ComparePreRelease(PreRelease.Split('.'), other.PreRelease.Split('.'));
    }

    /// <summary>
    ///     Reads a version or a release tag, such as "v1.2.0-rc.1" or "1.1.1-8+Branch.main".
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out AppVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string value = text.Trim();
        if (value[0] is 'v' or 'V')
        {
            value = value[1..];
        }

        int metadata = value.IndexOf('+');
        if (metadata >= 0)
        {
            value = value[..metadata];
        }

        int dash = value.IndexOf('-');
        string core = dash < 0 ? value : value[..dash];
        string preRelease = dash < 0 ? "" : value[(dash + 1)..];
        string[] numbers = core.Split('.');

        if (numbers.Length != 3
            || !TryParseNumber(numbers[0], out int major)
            || !TryParseNumber(numbers[1], out int minor)
            || !TryParseNumber(numbers[2], out int patch)
            || dash >= 0 && !preRelease.Split('.').All(IsIdentifier))
        {
            return false;
        }

        version = new AppVersion(major, minor, patch, preRelease);
        return true;
    }

    public override string ToString() => IsPreRelease ? $"{Major}.{Minor}.{Patch}-{PreRelease}" : $"{Major}.{Minor}.{Patch}";

    public static bool operator <(AppVersion left, AppVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(AppVersion left, AppVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(AppVersion left, AppVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(AppVersion left, AppVersion right) => left.CompareTo(right) >= 0;

    private static int ComparePreRelease(string[] left, string[] right)
    {
        for (int index = 0; index < Math.Min(left.Length, right.Length); index++)
        {
            bool isLeftNumber = TryParseNumber(left[index], out int leftNumber);
            bool isRightNumber = TryParseNumber(right[index], out int rightNumber);

            // Numeric identifiers compare as numbers and sort below alphanumeric ones.
            int order = isLeftNumber && isRightNumber ? leftNumber.CompareTo(rightNumber)
                : isLeftNumber ? -1
                : isRightNumber ? 1
                : string.CompareOrdinal(left[index], right[index]);

            if (order != 0)
            {
                return order;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    private static bool TryParseNumber(string text, out int number)
    {
        number = 0;
        return text.Length > 0 && text.All(char.IsAsciiDigit) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    private static bool IsIdentifier(string text) => text.Length > 0 && text.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
}
