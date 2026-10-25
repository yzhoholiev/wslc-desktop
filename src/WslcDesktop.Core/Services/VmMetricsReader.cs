using System.Globalization;
using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Reads resource usage of the wslc session VM from its /proc files.
/// </summary>
public sealed class VmMetricsReader(IRuntimeReader reader)
{
    private (long Busy, long Total)? _previousCpu;

    public async Task<VmMetrics> ReadAsync(CancellationToken ct) =>
        Parse(await reader.ReadVmStatusAsync(ct));

    /// <summary>
    ///     Parses /proc/loadavg, /proc/uptime, /proc/meminfo and /proc/stat in that order; CPU use is relative to the previous call.
    /// </summary>
    internal VmMetrics Parse(string output)
    {
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string loadAverage = lines.Length > 0 ? string.Join("  ", lines[0].Split(' ').Take(3)) : "";
        TimeSpan uptime = lines.Length > 1 && double.TryParse(lines[1].Split(' ')[0], CultureInfo.InvariantCulture, out double seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;

        var memInfo = lines
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => parts[0])
            .ToDictionary(group => group.Key, group => ParseKilobytes(group.First()[1]));

        string? cpuLine = lines.FirstOrDefault(line => line.StartsWith("cpu ", StringComparison.Ordinal));
        int virtualCpus = lines.Count(line => line.Length > 3 && line.StartsWith("cpu", StringComparison.Ordinal) && char.IsDigit(line[3]));

        return new VmMetrics(
            virtualCpus,
            cpuLine is null ? 0 : ComputeCpuPercent(cpuLine),
            memInfo.GetValueOrDefault("MemTotal"),
            memInfo.GetValueOrDefault("MemAvailable"),
            memInfo.GetValueOrDefault("SwapTotal"),
            memInfo.GetValueOrDefault("SwapFree"),
            loadAverage,
            uptime);
    }

    private double ComputeCpuPercent(string cpuLine)
    {
        // user nice system idle iowait irq softirq steal, in clock ticks since boot.
        long[] ticks = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Take(8)
            .Select(value => long.TryParse(value, out long parsed) ? parsed : 0)
            .ToArray();

        if (ticks.Length < 5)
        {
            return 0;
        }

        long total = ticks.Sum();
        long busy = total - ticks[3] - ticks[4];
        (long Busy, long Total)? previous = _previousCpu;
        _previousCpu = (busy, total);

        if (previous is not { } prior || total <= prior.Total)
        {
            return 0;
        }

        return Math.Clamp(100d * (busy - prior.Busy) / (total - prior.Total), 0, 100);
    }

    private static long ParseKilobytes(string value)
    {
        string number = value.Trim().Split(' ')[0];
        return long.TryParse(number, out long kilobytes) ? kilobytes * 1024 : 0;
    }
}
