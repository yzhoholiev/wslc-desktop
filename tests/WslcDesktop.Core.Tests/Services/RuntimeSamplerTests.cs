using System.Text.Json;
using WslcDesktop.Models;
using WslcDesktop.Services;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.Services;

public sealed class RuntimeSamplerTests
{
    private const string ProcFiles = """
                                     0.10 0.20 0.30 1/100 1
                                     120.5 100
                                     MemTotal: 1000 kB
                                     MemAvailable: 500 kB
                                     cpu  100 0 100 700 100 0 0 0 0 0
                                     cpu0 100 0 100 700 100 0 0 0 0 0
                                     """;

    private const string DockerStorage = """
                                         Filesystem 1B-blocks Used Available Use% Mounted on
                                         /dev/sdb 1000 400 600 40% /var/lib/docker
                                         """;

    private readonly TestServices _test = new();
    private bool _isLive = true;

    public RuntimeSamplerTests()
    {
        _test.Session.IsConnected = true;
        _test.Details.Info = Info(Wslc.DefaultSessionName);
        _test.Reader.Images = [new ImageDto { Size = "1MB", Containers = "0" }];
        _test.Reader.Volumes = [new VolumeDto { Name = "data" }];
        _test.Reader.Networks = [new NetworkDto { Name = "bridge" }, new NetworkDto { Name = "app" }];
        _test.Reader.VmStatus = ProcFiles;
        _test.Reader.StorageUsage = DockerStorage;
    }

    /// <summary>
    ///     Samples a stopped runtime until the sampler stops asking wslc: the first sample, then one per tick of the settling period.
    /// </summary>
    private static async Task<RuntimeSnapshot> SampleUntilSettledAsync(RuntimeSampler sampler)
    {
        RuntimeSnapshot snapshot = RuntimeSnapshot.Empty;
        for (int sample = 0; sample < 4; sample++)
        {
            snapshot = await sampler.SampleAsync(snapshot, false);
        }

        return snapshot;
    }

    private RuntimeSampler CreateSampler() => new(_test.Reader, _test.Session, _test.Details, _test.Hosts, () => _isLive);

    [Fact]
    public async Task SampleAsync_ReadsTheWholeRuntime()
    {
        _test.Reader.Containers = [Container("web"), Container("batch", "exited")];

        RuntimeSnapshot snapshot = await CreateSampler().SampleAsync(RuntimeSnapshot.Empty, false);

        Assert.True(snapshot.IsRunning);
        Assert.Null(snapshot.Error);
        Assert.Equal(["web", "batch"], snapshot.Containers.Select(container => container.Names));
        Assert.Equal(["web-id-0123456789abcdef"], snapshot.Stats.Keys);
        Assert.Equal(1, snapshot.Vm?.VirtualCpus);
        Assert.Equal(new Inventory(1, 1_000_000, 1, 1, 0, 2, 1, 1_000_000), snapshot.Inventory);
        Assert.Equal(new DiskUsage(1000, 400), snapshot.Storage);
    }

    [Fact]
    public async Task SampleAsync_CountsAnImageWithSeveralTagsOnce()
    {
        _test.Reader.Images =
        [
            new ImageDto { Id = "sha256:a", Repository = "app", Tag = "1.0", Size = "1MB", Containers = "0" },
            new ImageDto { Id = "sha256:a", Repository = "app", Tag = "latest", Size = "1MB", Containers = "0" },
            new ImageDto { Id = "sha256:b", Repository = "db", Tag = "16", Size = "2MB", Containers = "1" }
        ];

        RuntimeSnapshot snapshot = await CreateSampler().SampleAsync(RuntimeSnapshot.Empty, false);

        Assert.Equal(new Inventory(2, 3_000_000, 1, 1, 0, 2, 1, 1_000_000), snapshot.Inventory);
    }

    [Fact]
    public async Task SampleAsync_SkipsStatsWithoutRunningContainers()
    {
        _test.Reader.Containers = [Container("batch", "exited")];

        RuntimeSnapshot snapshot = await CreateSampler().SampleAsync(RuntimeSnapshot.Empty, false);

        Assert.Empty(snapshot.Stats);
        Assert.Equal(0, _test.Reader.StatsReads);
    }

    [Fact]
    public async Task SampleAsync_WithoutTheSessionReportsItStoppedAndClosesTheConnection()
    {
        RuntimeSampler sampler = CreateSampler();
        RuntimeSnapshot running = await sampler.SampleAsync(RuntimeSnapshot.Empty, false);

        _test.Details.ReadInfo = () => Info("someone-else");
        RuntimeSnapshot snapshot = await sampler.SampleAsync(running, true);

        Assert.False(snapshot.IsRunning);
        Assert.Empty(snapshot.Containers);
        Assert.NotNull(snapshot.Info);
        Assert.Equal(1, _test.Session.Disconnects);
    }

    [Fact]
    public async Task SampleAsync_ReadsInfoOnlyEveryTenthSampleWhileConnected()
    {
        RuntimeSampler sampler = CreateSampler();
        RuntimeSnapshot snapshot = RuntimeSnapshot.Empty;

        for (int sample = 1; sample <= 12; sample++)
        {
            snapshot = await sampler.SampleAsync(snapshot, false);
            int expected = sample switch
            {
                1 => 1,
                < 12 => 2,
                _ => 3
            };

            Assert.Equal(expected, _test.Details.InfoReads);
        }

        await sampler.SampleAsync(snapshot, true);
        Assert.Equal(4, _test.Details.InfoReads);
    }

    [Fact]
    public async Task SampleAsync_SkipsWslcOnceTheSessionHostsSettledWhileStopped()
    {
        _test.Details.Info = Info();
        RuntimeSampler sampler = CreateSampler();
        RuntimeSnapshot settled = await SampleUntilSettledAsync(sampler);

        RuntimeSnapshot next = await sampler.SampleAsync(settled, false);

        Assert.Same(settled, next);
        Assert.Equal(4, _test.Details.InfoReads);
    }

    [Fact]
    public async Task SampleAsync_ChecksWslcOnceASessionHostAppears()
    {
        _test.Details.Info = Info();
        RuntimeSampler sampler = CreateSampler();
        RuntimeSnapshot settled = await SampleUntilSettledAsync(sampler);

        _test.Hosts.Ids = [4242];
        _test.Details.Info = Info(Wslc.DefaultSessionName);
        RuntimeSnapshot started = await sampler.SampleAsync(settled, false);

        Assert.True(started.IsRunning);
    }

    [Fact]
    public async Task SampleAsync_KeepsCheckingWhileANewHostsSessionIsNotListedYet()
    {
        _test.Details.Info = Info();
        RuntimeSampler sampler = CreateSampler();
        RuntimeSnapshot snapshot = await SampleUntilSettledAsync(sampler);

        _test.Hosts.Ids = [4242];
        snapshot = await sampler.SampleAsync(snapshot, false);
        snapshot = await sampler.SampleAsync(snapshot, false);
        _test.Details.Info = Info(Wslc.DefaultSessionName);
        snapshot = await sampler.SampleAsync(snapshot, false);

        Assert.True(snapshot.IsRunning);
    }

    [Fact]
    public async Task SampleAsync_ChecksWslcWhenForcedEvenIfNoSessionHostChanged()
    {
        _test.Details.Info = Info();
        RuntimeSampler sampler = CreateSampler();
        RuntimeSnapshot settled = await SampleUntilSettledAsync(sampler);

        await sampler.SampleAsync(settled, true);

        Assert.Equal(5, _test.Details.InfoReads);
    }

    [Fact]
    public async Task SampleAsync_ChecksWslcAgainAfterAFailedReadWhileStopped()
    {
        _test.Details.Info = Info();
        RuntimeSampler sampler = CreateSampler();
        RuntimeSnapshot settled = await SampleUntilSettledAsync(sampler);

        await sampler.SampleAsync(settled with { Error = "wslc did not respond in time." }, false);

        Assert.Equal(5, _test.Details.InfoReads);
    }

    [Fact]
    public async Task SampleAsync_WaitsForTheHostsToSettleAgainAfterTheRuntimeRan()
    {
        _test.Details.Info = Info();
        RuntimeSampler sampler = CreateSampler();
        RuntimeSnapshot settled = await SampleUntilSettledAsync(sampler);
        _test.Hosts.Ids = [4242];
        _test.Details.Info = Info(Wslc.DefaultSessionName);
        RuntimeSnapshot running = await sampler.SampleAsync(settled, false);
        _test.Details.Info = Info();
        RuntimeSnapshot stopped = await sampler.SampleAsync(running, true);

        await sampler.SampleAsync(stopped, false);

        Assert.Equal(7, _test.Details.InfoReads);
    }

    [Fact]
    public async Task SampleAsync_ListsNoSessionHostsWhileRunning()
    {
        RuntimeSampler sampler = CreateSampler();

        await sampler.SampleAsync(await sampler.SampleAsync(RuntimeSnapshot.Empty, false), false);

        Assert.Equal(0, _test.Hosts.Lists);
    }

    [Fact]
    public async Task SampleAsync_ReadsInfoAgainWhileTheSessionIsDisconnected()
    {
        RuntimeSampler sampler = CreateSampler();
        RuntimeSnapshot first = await sampler.SampleAsync(RuntimeSnapshot.Empty, false);
        RuntimeSnapshot second = await sampler.SampleAsync(first, false);

        _test.Session.IsConnected = false;
        await sampler.SampleAsync(second, false);

        Assert.Equal(3, _test.Details.InfoReads);
    }

    [Fact]
    public async Task SampleAsync_ReadsTheInventoryEveryFifthSampleOrWhenForced()
    {
        RuntimeSampler sampler = CreateSampler();
        RuntimeSnapshot snapshot = RuntimeSnapshot.Empty;

        for (int sample = 1; sample <= 6; sample++)
        {
            snapshot = await sampler.SampleAsync(snapshot, false);
            Assert.Equal(sample < 6 ? 1 : 2, _test.Reader.ImageReads);
            Assert.NotNull(snapshot.Inventory);
        }

        await sampler.SampleAsync(snapshot, true);
        Assert.Equal(3, _test.Reader.ImageReads);
    }

    [Fact]
    public async Task ForgetInventory_ReadsItAgainOnTheNextSample()
    {
        RuntimeSampler sampler = CreateSampler();
        RuntimeSnapshot snapshot = await sampler.SampleAsync(RuntimeSnapshot.Empty, false);

        sampler.ForgetInventory();
        await sampler.SampleAsync(snapshot, false);

        Assert.Equal(2, _test.Reader.ImageReads);
    }

    [Fact]
    public async Task SampleAsync_ReportsAMissingWslcAndKeepsLookingForIt()
    {
        _test.Details.ReadInfo = () => throw new ToolNotFoundException("not found");
        RuntimeSampler sampler = CreateSampler();

        RuntimeSnapshot snapshot = await sampler.SampleAsync(RuntimeSnapshot.Empty, false);
        await sampler.SampleAsync(snapshot, false);

        Assert.True(snapshot.IsWslcMissing);
        Assert.Equal(Wslc.NotInstalledMessage, snapshot.Error);
        Assert.Null(snapshot.Info);
        Assert.Equal(2, _test.Details.InfoReads);
    }

    [Theory]
    [InlineData(false, "wslc broke")]
    [InlineData(true, "wslc did not respond in time.")]
    public async Task SampleAsync_KeepsThePreviousContainersButNotItsMeasurementsOnFailure(bool timesOut, string expected)
    {
        RuntimeSnapshot previous = Running([Container("web")], new Dictionary<string, ContainerStatsDto> { ["web"] = new() }) with
        {
            Vm = new VmMetrics(1, 5, 100, 50, 0, 0, "", TimeSpan.Zero)
        };

        _test.Reader.ContainersFailure = timesOut ? new OperationCanceledException() : new CliException("wslc broke");

        RuntimeSnapshot snapshot = await CreateSampler().SampleAsync(previous, false);

        Assert.Equal(expected, snapshot.Error);
        Assert.Same(previous.Containers, snapshot.Containers);
        Assert.Null(snapshot.Vm);
        Assert.Empty(snapshot.Stats);
    }

    [Theory]
    [InlineData(true, "Lost the connection to the session; reconnecting.")]
    [InlineData(false, null)]
    public async Task SampleAsync_ConfirmsTheSessionAfterItsShellEnded(bool isLive, string? expected)
    {
        _isLive = isLive;
        RuntimeSnapshot previous = Running([Container("web")], new Dictionary<string, ContainerStatsDto> { ["web"] = new() });
        _test.Reader.ContainersFailure = new SessionEndedException("shell ended");

        RuntimeSnapshot snapshot = await CreateSampler().SampleAsync(previous, false);

        Assert.Equal(expected, snapshot.Error);
        Assert.True(snapshot.IsRunning);
        Assert.Same(previous.Containers, snapshot.Containers);
        Assert.Empty(snapshot.Stats);
        Assert.Equal(2, _test.Details.InfoReads);
    }

    [Fact]
    public async Task SampleAsync_ReportsTheRuntimeStoppedWhenTheSessionEndedWithItsShell()
    {
        int calls = 0;
        _test.Details.ReadInfo = () => ++calls == 1 ? Info(Wslc.DefaultSessionName) : Info();
        _test.Reader.ContainersFailure = new SessionEndedException("shell ended");

        RuntimeSnapshot snapshot = await CreateSampler().SampleAsync(Running(Container("web")), false);

        Assert.False(snapshot.IsRunning);
        Assert.Null(snapshot.Error);
        Assert.Empty(snapshot.Containers);
    }

    [Fact]
    public async Task SampleAsync_LeavesOutVmMetricsItCannotRead()
    {
        _test.Reader.VmFailure = new CliException("cat failed");

        RuntimeSnapshot snapshot = await CreateSampler().SampleAsync(RuntimeSnapshot.Empty, false);

        Assert.Null(snapshot.Vm);
        Assert.Null(snapshot.Error);
        Assert.True(snapshot.IsRunning);
    }

    [Fact]
    public async Task SampleAsync_TreatsAShellEndingDuringTheVmReadAsALostConnection()
    {
        _test.Reader.VmFailure = new SessionEndedException("shell ended");

        RuntimeSnapshot snapshot = await CreateSampler().SampleAsync(Running(Container("web")), false);

        Assert.Equal("Lost the connection to the session; reconnecting.", snapshot.Error);
    }

    [Fact]
    public async Task SampleAsync_LeavesOutStorageItCannotRead()
    {
        _test.Reader.StorageFailure = new CliException("df failed");

        RuntimeSnapshot snapshot = await CreateSampler().SampleAsync(RuntimeSnapshot.Empty, false);

        Assert.Null(snapshot.Storage);
        Assert.NotNull(snapshot.Inventory);
        Assert.Null(snapshot.Error);
    }

    [Fact]
    public async Task SampleAsync_ReportsUnreadableOutput()
    {
        RuntimeSnapshot previous = Running(Container("web"));
        _test.Reader.ContainersFailure = new JsonException("bad json");

        RuntimeSnapshot snapshot = await CreateSampler().SampleAsync(previous, false);

        Assert.Equal("bad json", snapshot.Error);
        Assert.Same(previous.Containers, snapshot.Containers);
    }

    [Fact]
    public async Task SampleAsync_KeepsTheEarlierInventoryWhenRereadingItFails()
    {
        RuntimeSampler sampler = CreateSampler();
        RuntimeSnapshot first = await sampler.SampleAsync(RuntimeSnapshot.Empty, false);

        _test.Reader.ImagesFailure = new CliException("images failed");
        RuntimeSnapshot failed = await sampler.SampleAsync(first, true);
        _test.Reader.ImagesFailure = null;
        RuntimeSnapshot next = await sampler.SampleAsync(failed, false);

        Assert.Equal("images failed", failed.Error);
        Assert.Equal(first.Inventory, failed.Inventory);
        Assert.Equal(first.Inventory, next.Inventory);
        Assert.Equal(first.Storage, next.Storage);
    }
}
