using System.Diagnostics;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

/// <summary>
///     Runs real cmd.exe processes, which every Windows machine has, to cover process handling without wslc.
/// </summary>
public sealed class CliTests
{
    private static readonly string Cmd = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";

    [Fact]
    public async Task RunAsync_ReportsAFailingExitCode()
    {
        CliResult result = await Cli.RunAsync(Cmd, ["/c", "exit 3"], cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(3, result.ExitCode);
        Assert.Equal("Exited with code 3.", result.ErrorText);
    }

    [Fact]
    public async Task RunCheckedAsync_ThrowsWithStandardError()
    {
        CliException error = await Assert.ThrowsAsync<CliException>(() =>
            Cli.RunCheckedAsync(Cmd, ["/c", "echo something broke 1>&2 & exit 1"], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("something broke", error.Message);
    }

    [Fact]
    public async Task RunCheckedAsync_ReturnsStandardOutputOnSuccess()
    {
        string output = await Cli.RunCheckedAsync(Cmd, ["/c", "echo ok"], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("ok", output.Trim());
    }

    [Fact]
    public async Task RunAsync_PassesEnvironmentVariables()
    {
        CliResult result = await Cli.RunAsync(
            Cmd,
            ["/c", "echo %WSLC_DESKTOP_TEST%"],
            new Dictionary<string, string> { ["WSLC_DESKTOP_TEST"] = "from-test" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("from-test", result.StdOut.Trim());
    }

    [Fact]
    public async Task RunAsync_ThrowsCliExceptionForAMissingExecutable()
    {
        ToolNotFoundException error = await Assert.ThrowsAsync<ToolNotFoundException>(() =>
            Cli.RunAsync("wslc-desktop-no-such-tool.exe", [], cancellationToken: TestContext.Current.CancellationToken));

        Assert.StartsWith("Could not start 'wslc-desktop-no-such-tool.exe'", error.Message);
    }

    [Fact]
    public async Task RunAsync_KillsTheProcessAndFailsWhenItTimesOut()
    {
        var stopwatch = Stopwatch.StartNew();

        CliException error = await Assert.ThrowsAsync<CliException>(() =>
            Cli.RunAsync(
                Cmd,
                ["/c", "ping -n 30 127.0.0.1 >nul"],
                timeout: TimeSpan.FromMilliseconds(300),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("cmd did not finish within 1 second.", error.Message);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"The timeout took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task RunAsync_KillsTheProcessWhenCancelled()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Cli.RunAsync(Cmd, ["/c", "ping -n 30 127.0.0.1 >nul"], cancellationToken: cancellation.Token));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Cancellation took {stopwatch.Elapsed}.");
    }
}
