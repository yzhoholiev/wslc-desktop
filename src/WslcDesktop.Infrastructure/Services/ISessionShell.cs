namespace WslcDesktop.Services;

internal sealed record ShellResult(int ExitCode, string Output);

/// <summary>
///     A shell inside a wslc session that runs one command at a time.
/// </summary>
internal interface ISessionShell : IDisposable
{
    bool IsConnected { get; }

    Task<ShellResult> ExecuteAsync(string command, CancellationToken ct);
}
