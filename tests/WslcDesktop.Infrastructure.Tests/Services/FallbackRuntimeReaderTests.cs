using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class FallbackRuntimeReaderTests
{
    private readonly ManualClock _clock = new();
    private readonly List<FakeReader> _connections = [];
    private readonly FakeReader _fallback = new("cli");
    private Func<FakeReader> _nextConnection = () => new FakeReader("engine");

    private FallbackRuntimeReader CreateReader() => new(
        () =>
        {
            FakeReader connection = _nextConnection();
            _connections.Add(connection);
            return connection;
        },
        _fallback,
        _clock);

    [Fact]
    public async Task EngineUnavailable_FallsBackAndClosesTheConnection()
    {
        _nextConnection = () => new FakeReader("engine", new EngineUnavailableException("curl: (7)"));
        FallbackRuntimeReader reader = CreateReader();

        List<ContainerDto> containers = await reader.ListContainersAsync(TestContext.Current.CancellationToken);

        Assert.Equal("cli", Assert.Single(containers).Names);
        Assert.Equal(RuntimeDataSource.Cli, reader.Source);
        Assert.Equal("curl: (7)", reader.LastConnectionError);
        Assert.True(_connections[0].IsDisposed);
        Assert.False(reader.IsConnected);
    }

    [Fact]
    public async Task EngineUnavailable_PausesTheConnectionForAMinute()
    {
        _nextConnection = () => new FakeReader("engine", new EngineUnavailableException("down"));
        FallbackRuntimeReader reader = CreateReader();
        CancellationToken ct = TestContext.Current.CancellationToken;
        await reader.ListContainersAsync(ct);

        _nextConnection = () => new FakeReader("engine");
        _clock.Advance(TimeSpan.FromSeconds(59));
        await reader.ListContainersAsync(ct);
        Assert.Single(_connections);
        Assert.Equal(RuntimeDataSource.Cli, reader.Source);

        _clock.Advance(TimeSpan.FromSeconds(1));
        List<ContainerDto> containers = await reader.ListContainersAsync(ct);
        Assert.Equal(2, _connections.Count);
        Assert.Equal("engine", Assert.Single(containers).Names);
        Assert.Equal(RuntimeDataSource.Engine, reader.Source);
    }

    [Fact]
    public async Task OtherFailures_PropagateWithoutFallingBack()
    {
        _nextConnection = () => new FakeReader("engine", new SessionEndedException("session ended"));
        FallbackRuntimeReader reader = CreateReader();

        await Assert.ThrowsAsync<SessionEndedException>(() => reader.ListContainersAsync(TestContext.Current.CancellationToken));

        Assert.Equal(0, _fallback.Reads);
        Assert.Null(reader.LastConnectionError);
        Assert.False(_connections[0].IsDisposed);
    }

    [Fact]
    public async Task Disconnect_ClosesTheConnectionAndReconnectsOnTheNextRead()
    {
        FallbackRuntimeReader reader = CreateReader();
        CancellationToken ct = TestContext.Current.CancellationToken;
        await reader.ListContainersAsync(ct);

        reader.Disconnect();
        Assert.True(_connections[0].IsDisposed);
        Assert.Equal(RuntimeDataSource.None, reader.Source);
        Assert.False(reader.IsConnected);

        await reader.ListContainersAsync(ct);
        Assert.Equal(2, _connections.Count);
        Assert.Equal(RuntimeDataSource.Engine, reader.Source);
    }

    [Fact]
    public async Task EveryRead_GoesThroughTheChosenReader()
    {
        FallbackRuntimeReader reader = CreateReader();
        CancellationToken ct = TestContext.Current.CancellationToken;
        ContainerDto[] containers = [new() { Id = "a", State = "running" }];

        List<ContainerDto> listed = await reader.ListContainersAsync(ct);
        await reader.GetStatsAsync(containers, ct);
        await reader.ListImagesAsync(ct);
        await reader.ListVolumesAsync(ct);
        await reader.ListNetworksAsync(ct);
        await reader.GetImageConfigAsync("nginx", ct);
        string output = await reader.ReadVmStatusAsync(ct);
        await reader.ReadStorageUsageAsync(ct);

        Assert.Equal(["containers", "stats:a", "images", "volumes", "networks", "image:nginx", "vm-status", "storage"], Assert.Single(_connections).Calls);
        Assert.Equal("engine", Assert.Single(listed).Names);
        Assert.Equal("engine: vm-status", output);
        Assert.Equal(RuntimeDataSource.Engine, reader.Source);
        Assert.True(reader.IsConnected);
        Assert.Equal(0, _fallback.Reads);
    }

    [Fact]
    public async Task KeepConnectedOff_ClosesTheConnectionShortlyAfterTheLastRead()
    {
        FallbackRuntimeReader reader = CreateReader();
        reader.KeepConnected = false;

        await reader.ListContainersAsync(TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromSeconds(4));
        Assert.True(reader.IsConnected);

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(_connections[0].IsDisposed);
        Assert.False(reader.IsConnected);
        Assert.Equal(RuntimeDataSource.None, reader.Source);
    }

    [Fact]
    public async Task KeepConnectedOff_ReadsInQuickSuccessionShareOneConnection()
    {
        FallbackRuntimeReader reader = CreateReader();
        CancellationToken ct = TestContext.Current.CancellationToken;
        reader.KeepConnected = false;

        await reader.ListContainersAsync(ct);
        _clock.Advance(TimeSpan.FromSeconds(3));
        await reader.ListImagesAsync(ct);
        _clock.Advance(TimeSpan.FromSeconds(3));

        Assert.True(reader.IsConnected);
        Assert.Single(_connections);
    }

    [Fact]
    public async Task KeepConnectedTurnedOff_ClosesAnIdleConnection()
    {
        FallbackRuntimeReader reader = CreateReader();
        await reader.ListContainersAsync(TestContext.Current.CancellationToken);

        reader.KeepConnected = false;
        _clock.Advance(TimeSpan.FromSeconds(5));

        Assert.True(_connections[0].IsDisposed);
    }

    [Fact]
    public async Task KeepConnectedTurnedBackOn_KeepsTheConnection()
    {
        FallbackRuntimeReader reader = CreateReader();
        reader.KeepConnected = false;
        await reader.ListContainersAsync(TestContext.Current.CancellationToken);

        reader.KeepConnected = true;
        _clock.Advance(TimeSpan.FromSeconds(5));

        Assert.True(reader.IsConnected);
    }

    private sealed class ManualClock : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];
        private DateTimeOffset _now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, _now + dueTime);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            _now += by;
            foreach (ManualTimer timer in _timers.ToList())
            {
                timer.FireIfDue(_now);
            }
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, DateTimeOffset dueAt) : ITimer
    {
        private bool _done;

        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

        public void Dispose() => _done = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void FireIfDue(DateTimeOffset now)
        {
            if (_done || now < dueAt)
            {
                return;
            }

            _done = true;
            callback(state);
        }
    }

    private sealed class FakeReader(string name, Exception? failWith = null) : IRuntimeConnection
    {
        public List<string> Calls { get; } = [];

        public int Reads => Calls.Count;

        public bool IsDisposed { get; private set; }

        public bool IsConnected => !IsDisposed;

        public Task<List<ContainerDto>> ListContainersAsync(CancellationToken ct) =>
            Answer("containers", () => new List<ContainerDto> { new() { Names = name } });

        public Task<List<ContainerStatsDto>> GetStatsAsync(IReadOnlyList<ContainerDto> containers, CancellationToken ct) =>
            Answer($"stats:{string.Join(',', containers.Select(container => container.Id))}", () => new List<ContainerStatsDto>());

        public Task<List<ImageDto>> ListImagesAsync(CancellationToken ct) => Answer("images", () => new List<ImageDto>());

        public Task<List<VolumeDto>> ListVolumesAsync(CancellationToken ct) => Answer("volumes", () => new List<VolumeDto>());

        public Task<List<NetworkDto>> ListNetworksAsync(CancellationToken ct) => Answer("networks", () => new List<NetworkDto>());

        public Task<ImageConfigDto> GetImageConfigAsync(string image, CancellationToken ct) =>
            Answer($"image:{image}", () => new ImageConfigDto { WorkingDir = name });

        public Task<string> ReadVmStatusAsync(CancellationToken ct) => Answer("vm-status", () => $"{name}: vm-status");

        public Task<string> ReadStorageUsageAsync(CancellationToken ct) => Answer("storage", () => $"{name}: storage");

        public void Dispose() => IsDisposed = true;

        private Task<T> Answer<T>(string call, Func<T> value)
        {
            Calls.Add(call);
            return failWith is null ? Task.FromResult(value()) : Task.FromException<T>(failWith);
        }
    }
}
