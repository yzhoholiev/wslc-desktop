namespace WslcDesktop.ViewModels;

/// <summary>
///     One row of the dashboard's busiest containers; the shares are bar widths from 0 to 1.
/// </summary>
public sealed record ContainerUsage(string Name, string Image, string Cpu, double CpuShare, string Memory, double MemoryShare);
