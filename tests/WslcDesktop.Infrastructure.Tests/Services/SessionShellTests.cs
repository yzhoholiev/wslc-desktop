using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

/// <summary>
///     Drives Git for Windows' sh in place of the shell wslc starts inside the session.
/// </summary>
public sealed class SessionShellTests : IDisposable
{
    private static readonly string Sh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "sh.exe");

    private readonly SessionShell _shell = new("test-session", TimeSpan.FromSeconds(2), () => Cli.CreateStartInfo(Sh, []));

    public SessionShellTests() => Assert.SkipUnless(File.Exists(Sh), "Git for Windows is not installed.");

    public void Dispose() => _shell.Dispose();

    [Fact]
    public async Task ExecuteAsync_ReturnsTheOutputAndExitCode()
    {
        ShellResult result = await _shell.ExecuteAsync("echo one; echo two; false", TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(["one", "two"], result.Output.Split('\n').Select(line => line.Trim()));
    }

    [Fact]
    public async Task ExecuteAsync_ReusesOneShellAcrossCommands()
    {
        await _shell.ExecuteAsync("x=kept", TestContext.Current.CancellationToken);

        ShellResult result = await _shell.ExecuteAsync("echo $x", TestContext.Current.CancellationToken);

        Assert.Equal("kept", result.Output.Trim());
    }

    [Fact]
    public async Task ExecuteAsync_EndsAHungCommandAndStartsAFreshShell()
    {
        SessionEndedException error = await Assert.ThrowsAsync<SessionEndedException>(() =>
            _shell.ExecuteAsync("sleep 30", TestContext.Current.CancellationToken));
        Assert.Contains("did not answer within 2 seconds", error.Message);

        ShellResult next = await _shell.ExecuteAsync("echo back", TestContext.Current.CancellationToken);

        Assert.Equal("back", next.Output.Trim());
    }

    [Fact]
    public async Task ExecuteAsync_PassesTheCallersCancellationOn()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _shell.ExecuteAsync("sleep 30", cancellation.Token));
    }

    [Fact]
    public async Task ExecuteAsync_ReportsAShellThatExitedAndRestartsIt()
    {
        await Assert.ThrowsAsync<SessionEndedException>(() => _shell.ExecuteAsync("exit 3", TestContext.Current.CancellationToken));

        ShellResult next = await _shell.ExecuteAsync("echo again", TestContext.Current.CancellationToken);

        Assert.Equal("again", next.Output.Trim());
    }
}
