using WslcDesktop.Models;
using WslcDesktop.Services;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.Services;

public sealed class RuntimeMonitorTests
{
    private static readonly Dictionary<string, (RuntimeSnapshot Latest, bool Rarely)> SamplingCases = new()
    {
        ["not sampled yet"] = (RuntimeSnapshot.Empty, false),
        ["running"] = (Running(Container("web")), false),
        ["running, with a failed read"] = (Running() with { Error = "wslc did not respond in time." }, false),
        ["stopped"] = (Stopped(), true),
        ["stopped, with a failed read"] = (Stopped() with { Error = "wslc did not respond in time." }, false),
        ["wslc missing"] = (RuntimeSnapshot.Empty with { IsWslcMissing = true, Error = Wslc.NotInstalledMessage }, true)
    };

    private readonly TestServices _test = new();

    private RuntimeMonitor Monitor => _test.Services.Monitor;

    public static TheoryData<string> SamplingCaseNames { get; } = new(SamplingCases.Keys);

    public static TheoryData<string, int, string> PollCases { get; } = new()
    {
        { "running", 0, "Sample" },
        { "running", 5, "Sample" },
        { "stopped", 0, "ForcedSample" },
        { "stopped", 3, "Sample" },
        { "stopped", 10, "ForcedSample" },
        { "stopped, with a failed read", 3, "Sample" },
        { "wslc missing", 0, "ForcedSample" },
        { "wslc missing", 3, "Skip" }
    };

    [Fact]
    public void Publish_RecordsVmUsageInTheHistory()
    {
        Monitor.Publish(WithVm(10, 75));

        Assert.Equal([10.0], Monitor.CpuHistory);
        Assert.Equal([25.0], Monitor.MemoryHistory);
    }

    [Fact]
    public void Publish_MakesTheSnapshotLatestAndNotifies()
    {
        RuntimeSnapshot snapshot = Running(Container("web"));
        int notified = 0;
        Monitor.Updated += () => notified++;

        Monitor.Publish(snapshot);

        Assert.Same(snapshot, Monitor.Latest);
        Assert.Equal(1, notified);
    }

    [Fact]
    public void IsLive_NotifiesOnlyWhenItChanges()
    {
        List<string?> changed = [];
        Monitor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Monitor.IsLive = true;
        Monitor.IsLive = false;
        Monitor.IsLive = true;

        Assert.Equal([nameof(RuntimeMonitor.IsLive), nameof(RuntimeMonitor.IsLive)], changed);
    }

    [Fact]
    public void IsLive_KeepsTheSessionConnectedOnlyWhileLive()
    {
        Monitor.IsLive = false;
        Assert.False(_test.Session.KeepConnected);

        Monitor.IsLive = true;
        Assert.True(_test.Session.KeepConnected);
    }

    [Theory]
    [MemberData(nameof(SamplingCaseNames))]
    public void SamplesRarely_OnlyWhileNothingButAnOutsideChangeCanAlterTheSnapshot(string caseName)
    {
        (RuntimeSnapshot? latest, bool rarely) = SamplingCases[caseName];

        Assert.Equal(rarely, RuntimeMonitor.SamplesRarely(latest));
    }

    [Theory]
    [MemberData(nameof(PollCases))]
    public void NextPoll_AsksWslcEveryTickWhileRunningAndForcesAFullCheckEveryTenthWhileIdle(string caseName, int idleTick, string expected)
    {
        (RuntimeSnapshot? latest, bool _) = SamplingCases[caseName];

        Assert.Equal(expected, RuntimeMonitor.NextPoll(latest, idleTick).ToString());
    }

    private static RuntimeSnapshot WithVm(double cpu, long memoryAvailable) =>
        Running() with { Vm = new VmMetrics(2, cpu, 100, memoryAvailable, 0, 0, "0.00 0.00 0.00", TimeSpan.FromMinutes(1)) };
}
