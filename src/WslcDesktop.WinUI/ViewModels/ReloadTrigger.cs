using WslcDesktop.Models;

namespace WslcDesktop.ViewModels;

/// <summary>
///     Decides from the monitor's snapshots when a list that the monitor does not poll must reload.
/// </summary>
/// <param name="countIn">How many of the list's items the monitor's inventory counts.</param>
internal sealed class ReloadTrigger(Func<Inventory, int> countIn)
{
    private int? _count;
    private int _ownRefreshes;
    private bool? _wasRunning;
    private bool _wasUnavailable;

    /// <summary>
    ///     Whether the last remembered snapshot predates the monitor's first sample.
    /// </summary>
    public bool AwaitsFirstSample { get; private set; }

    /// <summary>
    ///     Records the state a load reflects, so that only a later change asks for another load.
    /// </summary>
    public void Remember(RuntimeSnapshot snapshot)
    {
        AwaitsFirstSample = !IsSampled(snapshot);
        _wasRunning = snapshot.IsRunning;
        _wasUnavailable = IsUnavailable(snapshot);
        _count = CountOf(snapshot) ?? _count;
    }

    /// <summary>
    ///     Records the snapshot and tells whether it changed what the list shows.
    /// </summary>
    public bool ShouldReload(RuntimeSnapshot snapshot)
    {
        int? count = CountOf(snapshot);
        bool firstSample = AwaitsFirstSample && IsSampled(snapshot);
        bool runningChanged = _wasRunning is { } wasRunning && wasRunning != snapshot.IsRunning;
        bool availabilityChanged = _wasRunning is not null && IsUnavailable(snapshot) != _wasUnavailable;
        bool countChanged = count is not null && _count is not null && count != _count && _ownRefreshes == 0;
        Remember(snapshot);
        return firstSample || runningChanged || availabilityChanged || countChanged;
    }

    /// <summary>
    ///     Ignores count changes until <see cref="EndOwnRefresh" />, for a monitor refresh that follows the list's own reload.
    /// </summary>
    public void BeginOwnRefresh() => _ownRefreshes++;

    public void EndOwnRefresh() => _ownRefreshes--;

    /// <summary>
    ///     Whether the monitor had sampled the runtime; before its first sample the runtime is unknown, not stopped.
    /// </summary>
    private static bool IsSampled(RuntimeSnapshot snapshot) => snapshot is not { Info: null, Error: null };

    private static bool IsUnavailable(RuntimeSnapshot snapshot) => snapshot is { Info: null, Error: not null };

    private int? CountOf(RuntimeSnapshot snapshot) => snapshot.Inventory is { } inventory ? countIn(inventory) : null;
}
