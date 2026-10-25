namespace WslcDesktop.Models;

public sealed record VmMetrics(
    int VirtualCpus,
    double CpuPercent,
    long MemoryTotal,
    long MemoryAvailable,
    long SwapTotal,
    long SwapFree,
    string LoadAverage,
    TimeSpan Uptime)
{
    public long MemoryUsed => MemoryTotal - MemoryAvailable;

    public double MemoryPercent => MemoryTotal == 0 ? 0 : 100d * MemoryUsed / MemoryTotal;

    public long SwapUsed => SwapTotal - SwapFree;
}

public sealed record DiskUsage(long Total, long Used);
