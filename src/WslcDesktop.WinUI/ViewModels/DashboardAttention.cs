using System.Text.RegularExpressions;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

public enum AttentionKind
{
    ContainerFailed,
    ContainerRestarting,
    UnusedImages
}

/// <summary>
///     Something on the dashboard that the user may want to act on.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="Key">Identifies the item among the dismissed ones.</param>
/// <param name="ContainerId">The container it is about, if any.</param>
/// <param name="Subject">The name shown in bold before <paramref name="Text" />, if any.</param>
/// <param name="Text">What happened, in words.</param>
/// <param name="Value">The amount a dismissal remembers, so that the item returns once it grows.</param>
public sealed record AttentionItem(AttentionKind Kind, string Key, string ContainerId, string Subject, string Text, long Value)
{
    public string Tone => Kind == AttentionKind.ContainerFailed ? "Critical" : "Caution";

    public string PrimaryAction => Kind == AttentionKind.UnusedImages ? "Clean up…" : "Logs";

    public string SecondaryAction => Kind switch
    {
        AttentionKind.ContainerFailed => "Start",
        AttentionKind.ContainerRestarting => "Stop",
        _ => ""
    };

    /// <summary>
    ///     A restart loop is still happening, so it stays until it ends.
    /// </summary>
    public bool CanDismiss => Kind != AttentionKind.ContainerRestarting;
}

/// <summary>
///     Decides what the dashboard's attention banner reports.
/// </summary>
public static partial class DashboardAttention
{
    public const string UnusedImagesKey = "unused-images";

    /// <summary>
    ///     Exit codes of a container that ended normally: 0, and the 137 and 143 a stop leaves behind (SIGKILL and SIGTERM).
    /// </summary>
    private static readonly int[] NormalExitCodes = [0, 137, 143];

    /// <summary>
    ///     The exit code the engine gives a container that was running when the runtime stopped under it.
    /// </summary>
    private const int InterruptedExitCode = 255;

    /// <summary>
    ///     The engine marks interrupted containers as it starts, which is shortly after the VM's uptime begins.
    /// </summary>
    private static readonly TimeSpan EngineStartAllowance = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     The items to report, most serious first, without the ones the user dismissed.
    /// </summary>
    /// <param name="snapshot">The latest reading of the runtime.</param>
    /// <param name="dismissed">Dismissed item keys with the value each had when dismissed.</param>
    public static AttentionItem[] Build(RuntimeSnapshot snapshot, IReadOnlyDictionary<string, long> dismissed)
    {
        List<AttentionItem> items = [];
        foreach (ContainerDto container in snapshot.Containers.OrderBy(container => container.Names, StringComparer.OrdinalIgnoreCase))
        {
            if (FailureOf(container, snapshot.Vm?.Uptime) is { } failure && !dismissed.ContainsKey(container.Id))
            {
                items.Add(new AttentionItem(AttentionKind.ContainerFailed, container.Id, container.Id, container.Names, failure, 0));
            }
        }

        foreach (ContainerDto container in snapshot.Containers.Where(container => container.State == "restarting"))
        {
            items.Add(new AttentionItem(AttentionKind.ContainerRestarting, container.Id, container.Id, container.Names, " keeps restarting", 0));
        }

        if (snapshot.Inventory is { UnusedImagesSize: > 0 } inventory
            && !(dismissed.TryGetValue(UnusedImagesKey, out long seen) && inventory.UnusedImagesSize <= seen))
        {
            string text = $"{ByteSize.Format(inventory.UnusedImagesSize)} in {Humanize.Count(inventory.UnusedImages, "unused image")}";
            items.Add(new AttentionItem(AttentionKind.UnusedImages, UnusedImagesKey, "", "", text, inventory.UnusedImagesSize));
        }

        return [.. items];
    }

    /// <summary>
    ///     Drops dismissals of containers that run again or are gone, so their next failure is reported.
    /// </summary>
    /// <returns>Whether any dismissal was dropped.</returns>
    public static bool Forget(RuntimeSnapshot snapshot, Dictionary<string, long> dismissed)
    {
        var failed = snapshot.Containers
            .Where(container => FailureOf(container, snapshot.Vm?.Uptime) is not null)
            .Select(container => container.Id)
            .ToHashSet();
        List<string> stale = [.. dismissed.Keys.Where(key => key != UnusedImagesKey && !failed.Contains(key))];
        foreach (string key in stale)
        {
            dismissed.Remove(key);
        }

        return stale.Count > 0;
    }

    /// <summary>
    ///     One figure per kind of item, which keeps the banner to a line however many items there are.
    /// </summary>
    public static string Summarize(IReadOnlyCollection<AttentionItem> items)
    {
        List<string> parts = [];
        int failed = items.Count(item => item.Kind == AttentionKind.ContainerFailed);
        int restarting = items.Count(item => item.Kind == AttentionKind.ContainerRestarting);
        if (failed > 0)
        {
            parts.Add($"{Humanize.Count(failed, "container")} failed");
        }

        if (restarting > 0)
        {
            parts.Add($"{restarting} restarting");
        }

        if (items.FirstOrDefault(item => item.Kind == AttentionKind.UnusedImages) is { } images)
        {
            parts.Add($"{ByteSize.Format(images.Value)} reclaimable");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>
    ///     How the container failed, as the text after its name, or null when it did not.
    /// </summary>
    /// <param name="container">The container to judge.</param>
    /// <param name="uptime">How long the runtime has been up, if known.</param>
    private static string? FailureOf(ContainerDto container, TimeSpan? uptime)
    {
        if (container.State != "exited" || ExitedPattern().Match(container.Status) is not { Success: true } match)
        {
            return null;
        }

        int code = int.Parse(match.Groups[1].ValueSpan);
        string when = match.Groups[2].Value;
        if (NormalExitCodes.Contains(code) || (code == InterruptedExitCode && uptime is { } up && ExitedByRuntimeStart(when, up)))
        {
            return null;
        }

        return when.Length > 0 ? $" exited with code {code} · {when}" : $" exited with code {code}";
    }

    /// <summary>
    ///     Whether a container may have exited no later than the runtime started, going by the rounded age in its status.
    /// </summary>
    private static bool ExitedByRuntimeStart(string when, TimeSpan uptime)
    {
        if (AgePattern().Match(when) is not { Success: true } match)
        {
            return false;
        }

        TimeSpan unit = match.Groups[2].Value switch
        {
            "second" => TimeSpan.FromSeconds(1),
            "minute" => TimeSpan.FromMinutes(1),
            "hour" => TimeSpan.FromHours(1),
            "day" => TimeSpan.FromDays(1),
            "week" => TimeSpan.FromDays(7),
            "month" => TimeSpan.FromDays(31),
            _ => TimeSpan.FromDays(366)
        };

        // The status rounds the age down, or to the nearest unit for "About a…", so the real age is below the next unit, or the one after.
        long units = match.Groups[1].Success ? long.Parse(match.Groups[1].ValueSpan) + 1 : 2;
        return unit * units + EngineStartAllowance >= uptime;
    }

    /// <summary>
    ///     A container status such as "Exited (137) 3 days ago": the exit code, then when it exited.
    /// </summary>
    [GeneratedRegex(@"^Exited \((\d{1,9})\)\s*(.*)$", RegexOptions.None, 1000)]
    private static partial Regex ExitedPattern();

    /// <summary>
    ///     The age in a container status, such as "26 seconds ago", "About an hour ago" or "Less than a second ago": the count if it has one, then the unit.
    /// </summary>
    [GeneratedRegex(@"^(?:(\d{1,9})|About an?|Less than a) (second|minute|hour|day|week|month|year)s? ago$", RegexOptions.None, 1000)]
    private static partial Regex AgePattern();
}
