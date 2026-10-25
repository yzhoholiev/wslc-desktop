using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class VmMetricsTests
{
    private static string ProcOutput(string cpuLine) => $"""
                                                         0.78 0.62 0.57 2/345 6789
                                                         1234.56 5678.90
                                                         MemTotal:       32000000 kB
                                                         MemFree:            1000 kB
                                                         MemAvailable:   30000000 kB
                                                         SwapTotal:       8000000 kB
                                                         SwapFree:        6000000 kB
                                                         {cpuLine}
                                                         cpu0 50 0 50 350 50 0 0 0 0 0
                                                         cpu1 50 0 50 350 50 0 0 0 0 0
                                                         intr 123 0 0
                                                         """;

    [Fact]
    public void Parse_ReadsLoadUptimeMemoryAndCpuCount()
    {
        VmMetrics metrics = new VmMetricsReader(new FakeRuntimeReader()).Parse(ProcOutput("cpu  100 0 100 700 100 0 0 0 0 0"));

        Assert.Equal(2, metrics.VirtualCpus);
        Assert.Equal("0.78  0.62  0.57", metrics.LoadAverage);
        Assert.Equal(1234.56, metrics.Uptime.TotalSeconds, Tolerance.Default);
        Assert.Equal(32_000_000L * 1024, metrics.MemoryTotal);
        Assert.Equal(2_000_000L * 1024, metrics.MemoryUsed);
        Assert.Equal(6.25, metrics.MemoryPercent, Tolerance.Default);
        Assert.Equal(2_000_000L * 1024, metrics.SwapUsed);
    }

    [Fact]
    public void Parse_ReportsCpuUseSinceThePreviousSample()
    {
        var reader = new VmMetricsReader(new FakeRuntimeReader());

        VmMetrics first = reader.Parse(ProcOutput("cpu  100 0 100 700 100 0 0 0 0 0"));
        // Busy ticks grew by 200 of 300 total: user 100→200, system 100→200, idle 700→800.
        VmMetrics second = reader.Parse(ProcOutput("cpu  200 0 200 800 100 0 0 0 0 0"));

        Assert.Equal(0, first.CpuPercent, Tolerance.Default);
        Assert.Equal(200d / 300 * 100, second.CpuPercent, Tolerance.Default);
    }

    [Fact]
    public void Parse_ToleratesMissingSections()
    {
        VmMetrics metrics = new VmMetricsReader(new FakeRuntimeReader()).Parse("");

        Assert.Equal(0, metrics.VirtualCpus);
        Assert.Equal(TimeSpan.Zero, metrics.Uptime);
        Assert.Equal(0, metrics.MemoryPercent, Tolerance.Default);
    }
}
