using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace WslcDesktop.Services;

/// <summary>
///     One long-lived shell inside a wslc session that runs commands sent over stdin.
/// </summary>
internal sealed class SessionShell(string session, TimeSpan commandTimeout, Func<ProcessStartInfo> createStartInfo) : ISessionShell
{
    /// <summary>
    ///     A shell in the named session; naming it makes wslc fail instead of creating the session, so the shell can never boot the VM by itself.
    /// </summary>
    public SessionShell(string session)
        : this(session, WslcCli.QueryTimeout, () => Cli.CreateStartInfo(WslcCli.ExecutablePath, WslcCli.SessionRunArguments(session, "sh")))
    {
    }

    private static readonly TimeSpan ExitGracePeriod = TimeSpan.FromSeconds(1);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly StringBuilder _stderr = new();
    private volatile bool _disposed;
    private Process? _process;

    public string Session { get; } = session;

    public bool IsConnected
    {
        get
        {
            try
            {
                return !_disposed && Volatile.Read(ref _process) is { HasExited: false };
            }
            catch (InvalidOperationException)
            {
                // Stop disposed the process on another thread.
                return false;
            }
        }
    }

    public async Task<ShellResult> ExecuteAsync(string command, CancellationToken ct)
    {
        if (_disposed)
        {
            throw Closed();
        }

        await _gate.WaitAsync(ct);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(commandTimeout);
        try
        {
            if (_disposed)
            {
                throw Closed();
            }

            lock (_stderr)
            {
                _stderr.Clear();
            }

            Process process = EnsureStarted();

            string marker = $"__wslc_desktop_{Guid.NewGuid():N}__";

            // Grouping keeps multi-command input to one exit status; the echo terminates output lacking a final newline.
            await process.StandardInput.WriteLineAsync($"{{ {command}\n}}; status=$?; echo; echo \"{marker} $status\"".AsMemory(), deadline.Token);
            await process.StandardInput.FlushAsync(deadline.Token);

            var output = new StringBuilder();
            while (true)
            {
                if (await process.StandardOutput.ReadLineAsync(deadline.Token) is not { } line)
                {
                    SessionEndedException ended = Ended();
                    await StopInBackgroundAsync();
                    throw ended;
                }

                if (!line.StartsWith(marker, StringComparison.Ordinal))
                {
                    output.AppendLine(line);
                    continue;
                }

                int exitCode = int.TryParse(line.AsSpan(marker.Length).Trim(), out int parsed) ? parsed : -1;
                return new ShellResult(exitCode, output.ToString().TrimEnd());
            }
        }
        catch (OperationCanceledException)
        {
            // The command may still be writing; its output would be read as the next command's, so start over.
            await StopInBackgroundAsync();
            if (ct.IsCancellationRequested)
            {
                throw;
            }

            throw new SessionEndedException($"The shell in session {Session} did not answer within {commandTimeout.TotalSeconds:0} seconds.");
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The pipes break when the process exits mid-command, or when Dispose stops it from another thread.
            await StopInBackgroundAsync();
            throw new SessionEndedException($"The shell in session {Session} ended: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    ///     Stops the shell; a command still running on another thread ends with a <see cref="SessionEndedException" />.
    /// </summary>
    public void Dispose()
    {
        // The gate is not disposed: a command in flight still releases it, and waiters must not hang.
        _disposed = true;
        Stop();
    }

    private Process EnsureStarted()
    {
        if (_process is { HasExited: false } running)
        {
            return running;
        }

        Stop();
        lock (_stderr)
        {
            _stderr.Clear();
        }

        var process = new Process { StartInfo = createStartInfo() };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not { Length: > 0 } text)
            {
                return;
            }

            lock (_stderr)
            {
                _stderr.AppendLine(text);
            }
        };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            process.Dispose();
            throw new SessionEndedException($"Could not start wslc: {ex.Message}");
        }

        process.BeginErrorReadLine();
        Interlocked.Exchange(ref _process, process);

        if (!_disposed)
        {
            return process;
        }

        // Dispose ran while the process started, so its Stop found nothing to stop.
        Stop();
        throw Closed();
    }

    private SessionEndedException Closed() => new($"The shell in session {Session} was closed.");

    private SessionEndedException Ended()
    {
        string details;
        lock (_stderr)
        {
            details = _stderr.ToString().Trim();
        }

        return new SessionEndedException(details.Length > 0 ? details : $"The shell in session {Session} ended.");
    }

    /// <summary>
    ///     Stops the shell without stalling the caller, usually the UI thread, while the process gets time to exit.
    /// </summary>
    private Task StopInBackgroundAsync() => Task.Run(Stop, CancellationToken.None);

    private void Stop()
    {
        if (Interlocked.Exchange(ref _process, null) is not { } process)
        {
            return;
        }

        try
        {
            if (process.HasExited)
            {
                return;
            }

            process.StandardInput.Close();
            if (!process.WaitForExit(ExitGracePeriod))
            {
                process.Kill(true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or Win32Exception or AggregateException)
        {
        }
        finally
        {
            process.Dispose();
        }
    }
}
