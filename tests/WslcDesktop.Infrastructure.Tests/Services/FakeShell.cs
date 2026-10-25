using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

/// <summary>
///     Session shell that answers each command with the reply registered for the first path the command contains.
/// </summary>
internal sealed class FakeShell : ISessionShell
{
    private readonly List<(string Path, ShellResult Reply)> _replies = [];

    public List<string> Commands { get; } = [];

    public object this[string path]
    {
        set
        {
            ShellResult reply = value as ShellResult ?? new ShellResult(0, (string)value);
            _replies.RemoveAll(entry => entry.Path == path);
            _replies.Add((path, reply));
        }
    }

    public bool IsConnected { get; private set; } = true;

    public Task<ShellResult> ExecuteAsync(string command, CancellationToken ct)
    {
        Commands.Add(command);
        (string Path, ShellResult Reply) match = _replies.FirstOrDefault(entry => command.Contains(entry.Path, StringComparison.Ordinal));
        return Task.FromResult(match.Reply ?? throw new InvalidOperationException($"No reply registered for: {command}"));
    }

    public void Dispose() => IsConnected = false;
}
