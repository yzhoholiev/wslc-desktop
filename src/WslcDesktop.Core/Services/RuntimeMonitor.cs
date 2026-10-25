using System.ComponentModel;
using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Periodically samples the wslc runtime and publishes snapshots on the context it was started with.
/// </summary>
public sealed class RuntimeMonitor : INotifyPropertyChanged
{
    /// <summary>
    ///     A missing wslc or a stopped runtime changes only from outside the app (its own actions refresh at once), so wslc is asked only every
    ///     30 s; in between, the session host processes show a start without starting anything.
    /// </summary>
    private const int RarelyEveryTicks = 10;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly MetricsHistory _history = new();
    private readonly RuntimeSampler _sampler;
    private readonly IRuntimeSession _session;
    private SynchronizationContext? _context;
    private CancellationTokenSource? _polling;

    public RuntimeMonitor(IRuntimeReader reader, IRuntimeSession session, IRuntimeDetails details, ISessionHosts hosts)
    {
        _session = session;
        _sampler = new RuntimeSampler(reader, session, details, hosts, () => IsLive);
        IsLive = true;
    }

    /// <summary>
    ///     Whether updates are live rather than paused by the user.
    /// </summary>
    public bool IsLive
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnIsLiveChanged(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLive)));
        }
    }

    public RuntimeSnapshot Latest { get; private set; } = RuntimeSnapshot.Empty;

    /// <summary>
    ///     Whether a sample has been published yet; until then <see cref="Latest" /> knows nothing about the runtime.
    /// </summary>
    public bool HasSampled => !ReferenceEquals(Latest, RuntimeSnapshot.Empty);

    /// <summary>
    ///     The operation on the whole runtime that is running, such as a start or a cleanup, if any.
    /// </summary>
    public string? Operation { get; private set; }

    /// <summary>
    ///     Whether the runtime is stopped or wslc is missing, which the monitor checks only rarely.
    /// </summary>
    public bool IsIdle => SamplesRarely(Latest);

    public double[] CpuHistory => _history.Cpu;

    public double[] MemoryHistory => _history.Memory;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Raised when a snapshot is published or the runtime operation changes.
    /// </summary>
    public event Action? Updated;

    /// <summary>
    ///     Starts polling and publishes every snapshot through the given context.
    /// </summary>
    public void Start(SynchronizationContext context)
    {
        _context = context;
        if (IsLive)
        {
            StartPolling();
        }
    }

    /// <summary>
    ///     Samples immediately, waiting for any sample already in progress.
    /// </summary>
    public Task RefreshAsync() => SampleAsync(true, true);

    private void StartPolling()
    {
        StopPolling();
        _polling = new CancellationTokenSource();
        _ = PollLoopAsync(_polling.Token);
    }

    private void StopPolling()
    {
        _polling?.Cancel();
        _polling?.Dispose();
        _polling = null;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Interval);
        int idleTicks = 0;
        try
        {
            do
            {
                PollAction action = NextPoll(Latest, idleTicks);
                if (SamplesRarely(Latest))
                {
                    idleTicks++;
                }

                if (action != PollAction.Skip)
                {
                    await SampleAsync(false, action == PollAction.ForcedSample);
                }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task SampleAsync(bool waitForGate, bool force)
    {
        if (waitForGate)
        {
            await _gate.WaitAsync();
        }
        else if (!await _gate.WaitAsync(0))
        {
            return;
        }

        try
        {
            RuntimeSnapshot snapshot;
            try
            {
                snapshot = await Task.Run(() => _sampler.SampleAsync(Latest, force));
            }
            catch (Exception ex)
            {
                // An unexpected failure must not end the poll loop or escape to a refresh's caller.
                CrashLog.Write(ex);
                snapshot = Latest.WithError(ex.Message);
            }

            if (_context is { } context)
            {
                var completion = new TaskCompletionSource();
                context.Post(_ =>
                {
                    try
                    {
                        Publish(snapshot);
                    }
                    finally
                    {
                        completion.TrySetResult();
                    }
                }, null);

                await completion.Task;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static bool SamplesRarely(RuntimeSnapshot latest) => latest.IsWslcMissing || latest.IsKnownStopped;

    /// <summary>
    ///     What the poll loop does on a tick: a running runtime is sampled every tick; a stopped one too, which the sampler answers from its
    ///     session hosts, with a full check every tenth tick; a missing wslc is looked for only on that tenth tick.
    /// </summary>
    internal static PollAction NextPoll(RuntimeSnapshot latest, int idleTick) =>
        !SamplesRarely(latest) ? PollAction.Sample
        : idleTick % RarelyEveryTicks == 0 ? PollAction.ForcedSample
        : latest.IsWslcMissing ? PollAction.Skip
        : PollAction.Sample;

    private void OnIsLiveChanged(bool value)
    {
        // An open session shell counts as activity and would keep the VM from idling out while updates are paused.
        _session.KeepConnected = value;

        // Before Start, there is no context to publish to yet.
        if (_context is null)
        {
            return;
        }

        if (value)
        {
            StartPolling();
        }
        else
        {
            StopPolling();
        }
    }

    /// <summary>
    ///     Records the runtime operation that started or ended, and notifies listeners.
    /// </summary>
    internal void SetOperation(string? operation)
    {
        Operation = operation;
        Updated?.Invoke();
    }

    /// <summary>
    ///     Makes a snapshot the latest one and notifies listeners.
    /// </summary>
    internal void Publish(RuntimeSnapshot snapshot)
    {
        _history.Record(snapshot);
        if (snapshot is { Vm: null, IsRunning: false })
        {
            _sampler.ForgetInventory();
        }

        Latest = snapshot;
        Updated?.Invoke();
    }

    internal enum PollAction
    {
        Skip,
        Sample,
        ForcedSample
    }
}
