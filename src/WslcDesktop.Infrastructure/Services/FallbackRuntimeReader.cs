using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Reads through a session connection while it works and through a fallback reader while it does not.
/// </summary>
internal sealed class FallbackRuntimeReader(Func<IRuntimeConnection> connect, IRuntimeReader fallback, TimeProvider time) : IRuntimeReader, IRuntimeSession
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    /// <summary>
    ///     Long enough to carry one sample's reads, which run back to back, over a single connection.
    /// </summary>
    private static readonly TimeSpan IdleDisconnectDelay = TimeSpan.FromSeconds(5);

    private readonly Lock _sync = new();
    private IRuntimeConnection? _connection;
    private ITimer? _idleTimer;
    private int _idleTimerGeneration;
    private bool _keepConnected = true;
    private int _reads;
    private DateTimeOffset _retryAt;

    public Task<List<ContainerDto>> ListContainersAsync(CancellationToken ct) => ReadAsync(reader => reader.ListContainersAsync(ct));

    public Task<List<ContainerStatsDto>> GetStatsAsync(IReadOnlyList<ContainerDto> containers, CancellationToken ct) =>
        ReadAsync(reader => reader.GetStatsAsync(containers, ct));

    public Task<List<ImageDto>> ListImagesAsync(CancellationToken ct) => ReadAsync(reader => reader.ListImagesAsync(ct));

    public Task<List<VolumeDto>> ListVolumesAsync(CancellationToken ct) => ReadAsync(reader => reader.ListVolumesAsync(ct));

    public Task<List<NetworkDto>> ListNetworksAsync(CancellationToken ct) => ReadAsync(reader => reader.ListNetworksAsync(ct));

    public Task<ImageConfigDto> GetImageConfigAsync(string image, CancellationToken ct) => ReadAsync(reader => reader.GetImageConfigAsync(image, ct));

    public Task<string> ReadVmStatusAsync(CancellationToken ct) => ReadAsync(reader => reader.ReadVmStatusAsync(ct));

    public Task<string> ReadStorageUsageAsync(CancellationToken ct) => ReadAsync(reader => reader.ReadStorageUsageAsync(ct));

    public string? LastConnectionError { get; private set; }

    public bool IsConnected => _connection?.IsConnected == true;

    public bool KeepConnected
    {
        get => _keepConnected;
        set
        {
            lock (_sync)
            {
                _keepConnected = value;
                if (!value && _reads == 0)
                {
                    ScheduleIdleDisconnect();
                }
            }
        }
    }

    public RuntimeDataSource Source { get; private set; }

    public void Disconnect()
    {
        IRuntimeConnection? connection;
        lock (_sync)
        {
            connection = _connection;
            _connection = null;
        }

        connection?.Dispose();
        Source = RuntimeDataSource.None;
    }

    private async Task<T> ReadAsync<T>(Func<IRuntimeReader, Task<T>> read)
    {
        IRuntimeConnection? connection = BeginRead();
        try
        {
            if (connection is not null)
            {
                try
                {
                    T value = await read(connection);
                    Source = RuntimeDataSource.Engine;
                    return value;
                }
                catch (EngineUnavailableException ex)
                {
                    // The session is fine but its engine API is not (e.g. a WSL update changed the session image).
                    Suspend(ex.Message);
                }
            }

            T fallbackValue = await read(fallback);
            Source = RuntimeDataSource.Cli;
            return fallbackValue;
        }
        finally
        {
            EndRead();
        }
    }

    private IRuntimeConnection? BeginRead()
    {
        lock (_sync)
        {
            _reads++;
            _idleTimer?.Dispose();
            _idleTimer = null;
            if (_connection is null && time.GetUtcNow() >= _retryAt)
            {
                _connection = connect();
            }

            return _connection;
        }
    }

    private void EndRead()
    {
        lock (_sync)
        {
            if (--_reads == 0 && !_keepConnected)
            {
                ScheduleIdleDisconnect();
            }
        }
    }

    private void ScheduleIdleDisconnect()
    {
        _idleTimer?.Dispose();
        int generation = ++_idleTimerGeneration;
        _idleTimer = time.CreateTimer(_ => DisconnectIfIdle(generation), null, IdleDisconnectDelay, Timeout.InfiniteTimeSpan);
    }

    private void DisconnectIfIdle(int generation)
    {
        IRuntimeConnection? connection;
        lock (_sync)
        {
            // A callback already queued when its timer was replaced must not cut the newer delay short.
            if (_reads > 0 || _keepConnected || generation != _idleTimerGeneration)
            {
                return;
            }

            connection = _connection;
            _connection = null;
        }

        connection?.Dispose();
        Source = RuntimeDataSource.None;
    }

    private void Suspend(string reason)
    {
        LastConnectionError = reason;
        lock (_sync)
        {
            _retryAt = time.GetUtcNow() + RetryDelay;
        }

        Disconnect();
    }
}
