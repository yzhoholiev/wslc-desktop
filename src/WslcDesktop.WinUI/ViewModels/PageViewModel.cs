using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

/// <summary>
///     Base of the page view models: the busy indicator and helpers that run, report and refresh after operations.
/// </summary>
public abstract partial class PageViewModel(AppServices services) : ObservableObject
{
    private int _busyCount;

    protected AppServices Services { get; } = services;

    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    [RelayCommand]
    public Task StartRuntimeAsync() => RunRuntimeOperationAsync(RuntimeActions.StartRuntimeDescription, Services.Actions.StartRuntimeAsync);

    public Task StopAllContainersAsync() =>
        RunRuntimeOperationAsync("Stopping all containers", Services.Actions.StopAllContainersAsync, "All containers stopped.");

    protected async Task WithBusyAsync(Func<Task> work)
    {
        SetBusy(+1);
        try
        {
            await work();
        }
        finally
        {
            SetBusy(-1);
        }
    }

    /// <summary>
    ///     Runs an operation on the whole runtime, such as a cleanup or a shutdown, unless another one is still running.
    /// </summary>
    protected async Task<bool> RunRuntimeOperationAsync(string description, Func<Task> operation, string? successMessage = null)
    {
        bool succeeded = false;
        await Services.Actions.RunExclusiveAsync(description, async () => succeeded = await RunOperationAsync(description, operation, successMessage));
        return succeeded;
    }

    /// <summary>
    ///     Runs an operation, then reloads what it may have changed, even after a failure.
    /// </summary>
    /// <param name="description">What the operation does, as notifications name it.</param>
    /// <param name="operation">The change to the runtime or to WSL.</param>
    /// <param name="successMessage">Shown when it succeeds, if any.</param>
    /// <param name="exclusive">Whether it acts on the whole runtime and must not overlap another such operation.</param>
    protected async Task RunAndReloadAsync(string description, Func<Task> operation, string? successMessage = null, bool exclusive = false)
    {
        await (exclusive
            ? RunRuntimeOperationAsync(description, operation, successMessage)
            : RunOperationAsync(description, operation, successMessage));

        await ReloadAsync();
    }

    /// <summary>
    ///     Reloads what the page shows after one of its operations.
    /// </summary>
    protected virtual Task ReloadAsync() => Services.Monitor.RefreshAsync();

    /// <summary>
    ///     Runs a user-initiated operation, reporting its outcome through <see cref="Notifier" />.
    /// </summary>
    protected async Task<bool> RunOperationAsync(string description, Func<Task> operation, string? successMessage = null)
    {
        SetBusy(+1);
        try
        {
            return await Services.Notifier.RunAsync(description, operation, successMessage);
        }
        finally
        {
            SetBusy(-1);
        }
    }

    /// <summary>
    ///     Shows the busy indicator for this page's own work and for a runtime operation started anywhere, such as a start from the tray.
    /// </summary>
    public void UpdateBusy() => IsBusy = IsWorking || Services.Monitor.Operation is not null;

    /// <summary>
    ///     Whether this page's own work is running, as opposed to a runtime operation started elsewhere.
    /// </summary>
    protected bool IsWorking => _busyCount > 0;

    private void SetBusy(int delta)
    {
        _busyCount += delta;
        UpdateBusy();
    }
}
