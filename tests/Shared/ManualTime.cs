namespace WslcDesktop.Tests;

/// <summary>
///     A clock that moves only when a test advances it, firing the timers that fall due.
/// </summary>
internal sealed class ManualTime : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state, _now + dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        DateTimeOffset target = _now + by;
        while (_timers.Where(timer => timer.Due <= target).MinBy(timer => timer.Due) is { } next)
        {
            _now = next.Due;
            next.Fire();
        }

        _now = target;
    }

    private sealed class ManualTimer(ManualTime owner, TimerCallback callback, object? state, DateTimeOffset due, TimeSpan period) : ITimer
    {
        public DateTimeOffset Due { get; private set; } = due;

        public bool Change(TimeSpan dueTime, TimeSpan newPeriod) => throw new NotSupportedException();

        public void Dispose() => owner._timers.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void Fire()
        {
            Due += period;
            callback(state);
        }
    }
}

/// <summary>
///     Runs posted work at once, standing in for the UI thread's context.
/// </summary>
internal sealed class ImmediateContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state) => d(state);
}
