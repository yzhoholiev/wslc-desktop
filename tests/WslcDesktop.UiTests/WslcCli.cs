using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text.Json;

namespace WslcDesktop.UiTests;

/// <summary>
///     The installed wslc, used to check what the app created, independently of the app's own code.
/// </summary>
internal static class WslcCli
{
    private static readonly string Executable = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WSL", "wslc.exe");

    /// <summary>
    ///     The session the app uses: wslc gives elevated and normal processes of one user separate sessions.
    /// </summary>
    public static string SessionName { get; } =
        (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) ? "wslc-cli-admin-" : "wslc-cli-")
        + Environment.UserName;

    public static async Task<string> RunAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;

        // wslc fails with ERROR_INVALID_HANDLE unless it has a stdin it can read.
        process.StandardInput.Close();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await process.WaitForExitAsync(timeout.Token);

        return process.ExitCode == 0
            ? await output
            : throw new InvalidOperationException($"wslc {string.Join(' ', arguments)} failed: {await error}{await output}");
    }

    public static async Task<bool> IsSessionRunningAsync()
    {
        using var info = JsonDocument.Parse(await RunAsync("info", "--format", "json"));
        return info.RootElement.GetProperty("Server").GetProperty("Sessions").EnumerateArray()
            .Any(session => session.GetProperty("Name").GetString() == SessionName);
    }

    /// <summary>
    ///     wslc's own view of a container: ports as published on Windows, mounts with Windows paths, and the run options it keeps.
    /// </summary>
    public static async Task<JsonElement> InspectAsync(string container)
    {
        using var document = JsonDocument.Parse(await RunAsync("inspect", container));
        return document.RootElement[0].Clone();
    }

    /// <summary>
    ///     The Docker Engine's view inside the session, which also holds what wslc inspect leaves out (TTY, stdin, hostname, GPUs).
    /// </summary>
    public static async Task<JsonElement> InspectInEngineAsync(string container)
    {
        string output = await RunAsync(
            "--session", SessionName, "system", "session", "run",
            "curl", "-sS", "--fail", "--unix-socket", "/var/run/docker.sock", $"http://localhost/v1.41/containers/{container}/json");

        using var document = JsonDocument.Parse(output);
        return document.RootElement.Clone();
    }

    public static async Task<bool> ContainerExistsAsync(string container) => (await ContainerNamesAsync()).Contains(container);

    public static async Task<string[]> ContainerNamesAsync() =>
        [.. (await ListAsync("list", "--all", "--no-trunc", "--format", "json")).Select(row => row.GetProperty("Names").GetString()!)];

    public static async Task<string[]> ImagesAsync() =>
        [.. (await ListAsync("images", "--format", "json")).Select(row => $"{row.GetProperty("Repository").GetString()}:{row.GetProperty("Tag").GetString()}")];

    public static async Task<string[]> VolumeNamesAsync() =>
        [.. (await ListAsync("volume", "list", "--format", "json")).Select(row => row.GetProperty("Name").GetString()!)];

    /// <summary>
    ///     The rows of a wslc list command, which prints one JSON object per line.
    /// </summary>
    private static async Task<JsonElement[]> ListAsync(params string[] arguments) =>
    [
        .. (await RunAsync(arguments)).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonDocument.Parse(line).RootElement.Clone())
    ];

    public static async Task TryAsync(params string[] arguments)
    {
        try
        {
            await RunAsync(arguments);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
