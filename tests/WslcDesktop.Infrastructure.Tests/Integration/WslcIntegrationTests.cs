using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Integration;

/// <summary>
///     Runs against the installed wslc and its default session; they start the session VM, so they only run when asked to.
/// </summary>
public sealed class WslcIntegrationTests
{
    private const string SkipReason = "Set WSLC_INTEGRATION=1 to run tests against the installed wslc.";

    private readonly CliRuntimeReader _cli = new();

    private readonly WslCli _wsl = new();
    private readonly WslcCli _wslc = new();

    public static bool Enabled => Environment.GetEnvironmentVariable("WSLC_INTEGRATION") == "1";

    [Fact(Skip = SkipReason, SkipUnless = nameof(Enabled))]
    public async Task GetInfoAsync_ReportsTheClientVersion()
    {
        WslcInfoDto info = await _wslc.GetInfoAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(info.Client.Version);
        Assert.NotEmpty(info.Server.SessionManagerVersion);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(Enabled))]
    public async Task SessionShell_RunsCommandsAndReportsExitCodes()
    {
        await _wslc.StartDefaultSessionAsync();
        using var shell = new SessionShell(Wslc.DefaultSessionName);
        CancellationToken ct = TestContext.Current.CancellationToken;

        Assert.Equal(new ShellResult(0, "hello"), await shell.ExecuteAsync("echo hello", ct));
        Assert.Equal(new ShellResult(0, "a\nb".ReplaceLineEndings()), await shell.ExecuteAsync("printf 'a\\nb'", ct));
        Assert.Equal(3, (await shell.ExecuteAsync("exit_code() { return 3; }; exit_code", ct)).ExitCode);
        Assert.Equal(1, (await shell.ExecuteAsync("false", ct)).ExitCode);
        Assert.True(shell.IsConnected);

        shell.Dispose();
        Assert.False(shell.IsConnected);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(Enabled))]
    public async Task DockerEngine_ListsWhatTheCliLists()
    {
        await _wslc.StartDefaultSessionAsync();
        using var shell = new SessionShell(Wslc.DefaultSessionName);
        var engine = new DockerEngine(shell);
        CancellationToken ct = TestContext.Current.CancellationToken;

        Assert.Equal(
            (await _cli.ListContainersAsync(ct)).Select(container => container.Id).Order(),
            (await engine.ListContainersAsync(ct)).Select(container => container.Id).Order());

        Assert.Equal(
            (await _cli.ListVolumesAsync(ct)).Select(volume => volume.Name).Order(),
            (await engine.ListVolumesAsync(ct)).Select(volume => volume.Name).Order());

        Assert.Equal(
            (await _cli.ListNetworksAsync(ct)).Select(network => network.Name).Order(),
            (await engine.ListNetworksAsync(ct)).Select(network => network.Name).Order());

        Assert.Equal(
            (await _cli.ListImagesAsync(ct)).Select(image => $"{image.Repository}:{image.Tag}").Order(),
            (await engine.ListImagesAsync(ct)).Select(image => $"{image.Repository}:{image.Tag}").Order());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(Enabled))]
    public async Task DockerEngine_ReportsStatsForRunningContainers()
    {
        await _wslc.StartDefaultSessionAsync();
        using var shell = new SessionShell(Wslc.DefaultSessionName);
        var engine = new DockerEngine(shell);
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<ContainerDto> containers = await engine.ListContainersAsync(ct);
        var running = containers.Where(container => container.IsRunning).Select(container => container.Id).ToList();
        if (running.Count == 0)
        {
            Assert.Skip("No running containers to sample.");
        }

        List<ContainerStatsDto> stats = await engine.GetStatsAsync(containers, ct);

        Assert.Equal(running.Order(), stats.Select(stat => stat.Id).Order());
        Assert.All(stats, stat => Assert.EndsWith("%", stat.CpuPerc));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(Enabled))]
    public async Task VmMetricsReader_ReadsTheSessionVm()
    {
        await _wslc.StartDefaultSessionAsync();

        var reader = new VmMetricsReader(DefaultServices.Create().Reader);
        VmMetrics metrics = await reader.ReadAsync(TestContext.Current.CancellationToken);

        Assert.True(metrics.VirtualCpus > 0);
        Assert.True(metrics.MemoryTotal > 0);
        Assert.True(metrics.Uptime > TimeSpan.Zero);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(Enabled))]
    public async Task ReadStorageUsageAsync_ReportsTheContainerStorage()
    {
        await _wslc.StartDefaultSessionAsync();

        string output = await DefaultServices.Create().Reader.ReadStorageUsageAsync(TestContext.Current.CancellationToken);

        Assert.Contains("1B-blocks", output, StringComparison.Ordinal);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(Enabled))]
    public async Task Wsl_ListsDistributionsAndVersions()
    {
        List<KeyValueEntry> versions = await _wsl.GetVersionInfoAsync(TestContext.Current.CancellationToken);

        Assert.Contains(versions, entry => entry.Key.StartsWith("WSL", StringComparison.Ordinal));
        _ = await _wsl.ListDistrosAsync(TestContext.Current.CancellationToken);
    }
}
