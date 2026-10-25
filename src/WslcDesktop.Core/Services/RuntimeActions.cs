namespace WslcDesktop.Services;

/// <summary>
///     Runtime and WSL operations that several parts of the app run.
/// </summary>
public sealed class RuntimeActions(
    IRuntimeCommands commands,
    IWslPlatform wsl,
    IRuntimeReader reader,
    IRuntimeSession session,
    RuntimeMonitor monitor,
    Notifier notifier)
{
    public const string StartRuntimeDescription = "Starting the container runtime";

    /// <summary>
    ///     Terminating stops containers gracefully, which took about 12 s for five containers.
    /// </summary>
    private static readonly TimeSpan ExitStopTimeout = TimeSpan.FromSeconds(30);

    private bool _isShuttingDownWsl;

    /// <summary>
    ///     Runs an operation on the whole runtime unless another one is still running.
    /// </summary>
    public async Task<bool> RunExclusiveAsync(string description, Func<Task> operation)
    {
        if (monitor.Operation is not null)
        {
            notifier.Error($"{description} did not start", "Another operation on the runtime is still running.");
            return false;
        }

        try
        {
            // Held by the monitor rather than the caller, which may be gone before the operation ends, and shown by every page.
            monitor.SetOperation(description);
            await operation();
            return true;
        }
        finally
        {
            monitor.SetOperation(null);
        }
    }

    public async Task StartRuntimeAsync()
    {
        await commands.StartDefaultSessionAsync();
        await monitor.RefreshAsync();
    }

    /// <summary>
    ///     Starts the runtime and reports the outcome, for callers with nowhere to show its progress.
    /// </summary>
    public Task StartRuntimeInBackgroundAsync() =>
        notifier.RunAsync(StartRuntimeDescription, () => RunExclusiveAsync(StartRuntimeDescription, StartRuntimeAsync));

    public async Task StopAllContainersAsync()
    {
        try
        {
            var running = (await reader.ListContainersAsync(CancellationToken.None))
                .Where(container => container.IsRunning)
                .Select(container => container.Id)
                .ToList();

            await commands.StopContainersAsync(running);
        }
        finally
        {
            // Some containers may have stopped before a failure.
            await monitor.RefreshAsync();
        }
    }

    /// <summary>
    ///     Shuts down WSL, its distributions and the runtime; not exclusive, since it is what ends a hung runtime operation.
    /// </summary>
    public async Task ShutdownWslAsync()
    {
        // The dashboard, the WSL page and the tray all offer it, and a second shutdown would only repeat the first.
        if (_isShuttingDownWsl)
        {
            throw new CliException("WSL is already shutting down.");
        }

        _isShuttingDownWsl = true;
        try
        {
            await wsl.ShutdownAsync();
            await monitor.RefreshAsync();
        }
        finally
        {
            _isShuttingDownWsl = false;
        }
    }

    /// <summary>
    ///     Updates WSL and reports what wsl.exe printed.
    /// </summary>
    public async Task UpdateWslAsync(bool preRelease)
    {
        string output = await wsl.UpdateAsync(preRelease);
        notifier.Success(string.IsNullOrWhiteSpace(output) ? "WSL is up to date." : output.Trim());
    }

    /// <summary>
    ///     Terminates this app's wslc session, which stops its containers and releases the VM.
    /// </summary>
    public async Task StopRuntimeAsync()
    {
        // The open session shell would otherwise report the termination as a lost connection.
        await Task.Run(session.Disconnect);
        await commands.TerminateSessionAsync(Wslc.DefaultSessionName);
        await monitor.RefreshAsync();
    }

    /// <summary>
    ///     Stops the runtime on the way out, bounded so a hung wslc cannot keep the app from closing.
    /// </summary>
    public async Task StopRuntimeOnExitAsync()
    {
        if (!monitor.Latest.IsRunning)
        {
            return;
        }

        try
        {
            session.Disconnect();
            await commands.TerminateSessionAsync(Wslc.DefaultSessionName, ExitStopTimeout);
        }
        catch (CliException ex)
        {
            CrashLog.Write(ex);
        }
    }
}
