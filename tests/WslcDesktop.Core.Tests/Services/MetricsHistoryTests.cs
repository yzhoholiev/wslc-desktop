using WslcDesktop.Models;
using WslcDesktop.Services;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.Services;

public sealed class MetricsHistoryTests
{
    private readonly MetricsHistory _history = new();

    [Fact]
    public void Record_AppendsCpuAndMemoryUse()
    {
        _history.Record(WithVm(10, 75));
        _history.Record(WithVm(20, 50));

        Assert.Equal([10.0, 20.0], _history.Cpu);
        Assert.Equal([25.0, 50.0], _history.Memory);
    }

    [Fact]
    public void Record_KeepsTheLastSixtySamples()
    {
        for (int cpu = 0; cpu < 61; cpu++)
        {
            _history.Record(WithVm(cpu, 50));
        }

        Assert.Equal(60, _history.Cpu.Length);
        Assert.Equal(1, _history.Cpu[0], Tolerance.Default);
        Assert.Equal(60, _history.Cpu[^1], Tolerance.Default);
        Assert.Equal(60, _history.Memory.Length);
    }

    [Fact]
    public void Record_LeavesTheHistoryAloneForARunningSnapshotWithoutMeasurements()
    {
        _history.Record(WithVm(10, 75));

        _history.Record(Running());

        Assert.Equal([10.0], _history.Cpu);
        Assert.Equal([25.0], _history.Memory);
    }

    [Fact]
    public void Record_StartsOverOnceTheRuntimeStops()
    {
        _history.Record(WithVm(10, 75));

        _history.Record(Stopped());

        Assert.Empty(_history.Cpu);
        Assert.Empty(_history.Memory);
    }

    private static RuntimeSnapshot WithVm(double cpu, long memoryAvailable) =>
        Running() with { Vm = new VmMetrics(2, cpu, 100, memoryAvailable, 0, 0, "", TimeSpan.Zero) };
}
