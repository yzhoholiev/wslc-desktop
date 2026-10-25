using System.IO;

namespace WslcDesktop.UiTests;

/// <summary>
///     A running container runtime with the images, network and files the end-to-end tests use.
/// </summary>
public sealed class RuntimeFixture : IAsyncLifetime
{
    public const string Prefix = "wslcdesktop-e2e-";

    /// <summary>
    ///     An image whose default command is a shell, so the dialog ticks TTY and stdin for it.
    /// </summary>
    public const string ShellImage = "busybox:latest";

    /// <summary>
    ///     An image that exposes a port and starts no shell.
    /// </summary>
    public const string ServerImage = "nginx:alpine";

    /// <summary>
    ///     A small image whose libraries wslc's GPU hook can index; it runs /sbin/ldconfig in the container, which busybox lacks.
    /// </summary>
    public const string GpuImage = "alpine:3";

    public const string Network = Prefix + "net";

    private bool _startedSession;

    public static bool Enabled => Environment.GetEnvironmentVariable("WSLC_E2E") == "1";

    public string HostFolder { get; } = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N")[..8]);

    public string EnvironmentFile => Path.Combine(HostFolder, "variables.env");

    public async ValueTask InitializeAsync()
    {
        if (!Enabled)
        {
            return;
        }

        _startedSession = !await WslcCli.IsSessionRunningAsync();

        // Listing starts the session when it is not running.
        await WslcCli.RunAsync("list", "--quiet");
        string[] images = await WslcCli.ImagesAsync();
        foreach (string image in new[] { ShellImage, ServerImage, GpuImage }.Where(image => !images.Contains(image)))
        {
            await WslcCli.RunAsync("pull", image);
        }

        await RemoveLeftoversAsync();
        await WslcCli.RunAsync("network", "create", Network);
        Directory.CreateDirectory(HostFolder);
        await File.WriteAllTextAsync(EnvironmentFile, "FROM_FILE=yes\n");
    }

    public async ValueTask DisposeAsync()
    {
        if (!Enabled)
        {
            return;
        }

        await RemoveLeftoversAsync();
        Directory.Delete(HostFolder, true);
        if (_startedSession)
        {
            await WslcCli.TryAsync("--session", WslcCli.SessionName, "system", "session", "terminate");
        }
    }

    /// <summary>
    ///     A container name no other test uses, so a failed test's container cannot be mistaken for another's.
    /// </summary>
    public static string NewName(string feature) => $"{Prefix}{feature}-{Guid.NewGuid().ToString("N")[..6]}";

    private static async Task RemoveLeftoversAsync()
    {
        foreach (string name in (await WslcCli.ContainerNamesAsync()).Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)))
        {
            await WslcCli.TryAsync("remove", "--force", name);
        }

        foreach (string name in (await WslcCli.VolumeNamesAsync()).Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)))
        {
            await WslcCli.TryAsync("volume", "remove", name);
        }

        await WslcCli.TryAsync("network", "remove", Network);
    }
}
