using System.Text.Json;
using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Reads one snapshot of the runtime through wslc and the session connection.
/// </summary>
internal sealed class RuntimeSampler
{
    private const int InventoryEveryTicks = 5;

    /// <summary>
    ///     While the session shell answers, the session is known to be up; info is only needed for the session list and versions.
    /// </summary>
    private const int InfoEveryTicks = 10;

    /// <summary>
    ///     A starting session's wslcsession.exe can appear before wslc info lists it, so a stopped runtime is checked this many more samples
    ///     (about 9 s) after the hosts change.
    /// </summary>
    private const int ChecksAfterHostsChange = 3;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private readonly IRuntimeDetails _details;
    private readonly ISessionHosts _hosts;

    private readonly InventoryReader _inventory;
    private readonly Func<bool> _isLive;
    private readonly IRuntimeReader _reader;
    private readonly IRuntimeSession _session;
    private readonly VmMetricsReader _vm;
    private int _checksSinceHostsChanged;
    private int[]? _hostsWhileStopped;
    private int _infoTick;
    private int _tick;

    public RuntimeSampler(IRuntimeReader reader, IRuntimeSession session, IRuntimeDetails details, ISessionHosts hosts, Func<bool> isLive)
    {
        _reader = reader;
        _session = session;
        _details = details;
        _hosts = hosts;
        _isLive = isLive;
        _vm = new VmMetricsReader(reader);
        _inventory = new InventoryReader(reader);
    }

    /// <summary>
    ///     Forgets the counted inventory, which a stopped runtime no longer has.
    /// </summary>
    public void ForgetInventory() => _inventory.Forget();

    /// <summary>
    ///     Reads a snapshot, carrying over from the previous one what this sample does not read again.
    /// </summary>
    /// <param name="previous">The latest published snapshot.</param>
    /// <param name="force">Whether to read everything now instead of at the usual intervals.</param>
    public async Task<RuntimeSnapshot> SampleAsync(RuntimeSnapshot previous, bool force)
    {
        if (IsUnchangedWhileStopped(previous, force))
        {
            return previous;
        }

        bool includeInventory = force || _tick++ % InventoryEveryTicks == 0;
        using var timeout = new CancellationTokenSource(Timeout);
        CancellationToken ct = timeout.Token;

        try
        {
            bool needsInfo = force || previous.Info is null || !_session.IsConnected || _infoTick++ % InfoEveryTicks == 0;
            WslcInfoDto info = needsInfo ? await _details.GetInfoAsync(ct) : previous.Info!;
            if (!RuntimeSnapshot.HasDefaultSession(info))
            {
                // Querying containers would start a session and keep the VM from idling out.
                _session.Disconnect();
                return RuntimeSnapshot.Empty with { Info = info };
            }

            // Reads share one session shell, which runs commands one at a time, so they are issued in sequence.
            List<ContainerDto> containers = await _reader.ListContainersAsync(ct);
            List<ContainerStatsDto> stats = containers.Any(container => container.IsRunning)
                ? await _reader.GetStatsAsync(containers, ct)
                : [];

            VmMetrics? vm = await TryReadVmAsync(ct);

            if (includeInventory || _inventory.Current is null)
            {
                await _inventory.RefreshAsync(ct);
            }

            return new RuntimeSnapshot(
                info,
                containers,
                stats.GroupBy(stat => stat.Id).ToDictionary(group => group.Key, group => group.First()),
                vm,
                _inventory.Storage,
                _inventory.Current,
                null);
        }
        catch (ToolNotFoundException)
        {
            return RuntimeSnapshot.Empty with { Error = Wslc.NotInstalledMessage, IsWslcMissing = true };
        }
        catch (SessionEndedException) when (!ct.IsCancellationRequested)
        {
            // The shell restarts on the next read; closing it here would also fail another read waiting on it.
        }
        catch (Exception ex) when (ex is CliException or OperationCanceledException or JsonException)
        {
            return WithPollError(previous, ex);
        }

        return await ConfirmSessionAfterShellEndedAsync(previous, ct);
    }

    /// <summary>
    ///     A stopped runtime starts only with a new wslcsession process, so while the set of them stays the same, wslc is not asked again.
    /// </summary>
    private bool IsUnchangedWhileStopped(RuntimeSnapshot previous, bool force)
    {
        if (!previous.IsKnownStopped)
        {
            _hostsWhileStopped = null;
            return false;
        }

        int[] hosts = _hosts.ListProcessIds();
        if (_hostsWhileStopped is not { } known || !hosts.SequenceEqual(known))
        {
            _hostsWhileStopped = hosts;
            _checksSinceHostsChanged = 0;
        }

        if (!force && _checksSinceHostsChanged >= ChecksAfterHostsChange)
        {
            return true;
        }

        _checksSinceHostsChanged++;
        return false;
    }

    /// <summary>
    ///     Checks whether the session is still there after its shell ended, without the CLI fallback, which would start a new one.
    /// </summary>
    private async Task<RuntimeSnapshot> ConfirmSessionAfterShellEndedAsync(RuntimeSnapshot previous, CancellationToken ct)
    {
        try
        {
            WslcInfoDto info = await _details.GetInfoAsync(ct);
            return RuntimeSnapshot.HasDefaultSession(info)
                // While paused no poll follows to clear the error, so it would outlive the reconnection.
                ? (previous with { Info = info }).WithError(_isLive() ? "Lost the connection to the session; reconnecting." : null)
                : RuntimeSnapshot.Empty with { Info = info };
        }
        catch (Exception ex) when (ex is CliException or OperationCanceledException or JsonException)
        {
            return WithPollError(previous, ex);
        }
    }

    private static RuntimeSnapshot WithPollError(RuntimeSnapshot previous, Exception ex) =>
        previous.WithError(ex is OperationCanceledException ? "wslc did not respond in time." : ex.Message);

    private async Task<VmMetrics?> TryReadVmAsync(CancellationToken ct)
    {
        try
        {
            return await _vm.ReadAsync(ct);
        }
        catch (CliException ex) when (ex is not SessionEndedException)
        {
            return null;
        }
    }
}
