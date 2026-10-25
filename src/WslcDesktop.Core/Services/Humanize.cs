namespace WslcDesktop.Services;

public static class Humanize
{
    private const int ShortIdLength = 12;

    /// <summary>
    ///     Relative age in the style of the Docker CLI, e.g. "4 days ago".
    /// </summary>
    public static string Ago(DateTimeOffset time)
    {
        TimeSpan age = DateTimeOffset.UtcNow - time;
        return age switch
        {
            { TotalSeconds: < 60 } => "Less than a minute ago",
            { TotalMinutes: < 60 } => Units((int)age.TotalMinutes, "minute"),
            { TotalHours: < 48 } => Units((int)age.TotalHours, "hour"),
            { TotalDays: < 14 } => Units((int)age.TotalDays, "day"),
            { TotalDays: < 60 } => Units((int)(age.TotalDays / 7), "week"),
            { TotalDays: < 730 } => Units((int)(age.TotalDays / 30), "month"),
            _ => Units((int)(age.TotalDays / 365), "year")
        };
    }

    /// <summary>
    ///     "1 container", "5 containers".
    /// </summary>
    public static string Count(int count, string singular) => count == 1 ? $"1 {singular}" : $"{count} {singular}s";

    /// <summary>
    ///     The ID cut to the length the Docker CLI shows by default.
    /// </summary>
    public static string ShortId(string id) => id.Length > ShortIdLength ? id[..ShortIdLength] : id;

    private static string Units(int count, string unit) => $"{Count(count, unit)} ago";
}
