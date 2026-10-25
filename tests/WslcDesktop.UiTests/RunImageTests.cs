using System.Text.Json;
using static WslcDesktop.UiTests.AppSession;
using static WslcDesktop.UiTests.RuntimeFixture;

namespace WslcDesktop.UiTests;

/// <summary>
///     Runs every option of the run-image dialog against the real runtime and checks the container wslc ends up with.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class RunImageTests(AppSession app, RuntimeFixture runtime) : IAsyncLifetime
{
    private const string SkipReason = "Set WSLC_E2E=1 (with WSLC Desktop closed) to run containers through the published app; it starts the runtime.";

    private const string KeepRunning = "sleep 300";

    private readonly List<string> _containers = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await RunDialog.CloseAnyAsync(app);
        foreach (string name in _containers)
        {
            await WslcCli.TryAsync("remove", "--force", name);
        }
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task Name_NamesTheContainer()
    {
        string name = await RunAsync("name", _ => Task.CompletedTask);

        Assert.Equal($"/{name}", (await WslcCli.InspectAsync(name)).GetProperty("Name").GetString());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task Command_ReplacesTheImageCommand()
    {
        string name = await RunAsync("command", _ => Task.CompletedTask, command: "sleep 301");

        Assert.Equal(["sleep", "301"], Strings((await WslcCli.InspectAsync(name)).GetProperty("Config").GetProperty("Cmd")));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task Entrypoint_ReplacesTheImageEntrypoint()
    {
        string name = await RunAsync("entrypoint", InMore(dialog => dialog.SetText("Entrypoint", "/bin/sh")), command: "-c \"sleep 300\"");

        JsonElement config = (await WslcCli.InspectAsync(name)).GetProperty("Config");
        Assert.Equal(["/bin/sh"], Strings(config.GetProperty("Entrypoint")));
        Assert.Equal(["-c", "sleep 300"], Strings(config.GetProperty("Cmd")));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task Port_PublishesTheContainerPortOnTheHost()
    {
        string name = await RunAsync("port", dialog => dialog.AddRowAsync("Add port", "Host port", "18431", "Container port, e.g. 80/tcp", "80/tcp"));

        JsonElement binding = (await WslcCli.InspectAsync(name)).GetProperty("Ports").GetProperty("80/tcp")[0];
        Assert.Equal("18431", binding.GetProperty("HostPort").GetString());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task PublishAll_PublishesTheExposedPortsToRandomHostPorts()
    {
        string name = await RunAsync(
            "publish-all", InMore(dialog => dialog.SetChecked("Publish every exposed port to a random host port", true)), ServerImage, "");

        JsonElement bindings = (await WslcCli.InspectAsync(name)).GetProperty("Ports").GetProperty("80/tcp");
        Assert.NotEqual(0, bindings.GetArrayLength());
        Assert.False(string.IsNullOrEmpty(bindings[0].GetProperty("HostPort").GetString()));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task Volume_MountsANamedVolume()
    {
        string volume = NewName("volume");
        string name =
            await RunAsync("volume", dialog => dialog.AddRowAsync("Add volume", "Host folder or volume name", volume, "Path in the container", "/data"));

        Assert.Contains(Mounts(await WslcCli.InspectAsync(name)), mount => mount is { Type: "volume", Destination: "/data" } && mount.Name == volume);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task Volume_MountsAHostFolder()
    {
        string name = await RunAsync("folder",
            dialog => dialog.AddRowAsync("Add volume", "Host folder or volume name", runtime.HostFolder, "Path in the container", "/host"));

        Assert.Contains(Mounts(await WslcCli.InspectAsync(name)), mount =>
            mount is { Type: "bind", Destination: "/host" } &&
            string.Equals(mount.Source.TrimEnd('\\'), runtime.HostFolder, StringComparison.OrdinalIgnoreCase));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task EnvironmentVariable_IsSetAsTyped()
    {
        string name = await RunAsync("env", dialog => dialog.AddRowAsync("Add variable", "Name", "E2E_KEY", "Value", "value with spaces"));

        Assert.Contains("E2E_KEY=value with spaces", Variables(await WslcCli.InspectAsync(name)));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task EnvironmentFile_SetsItsVariables()
    {
        string name = await RunAsync("env-file", InMore(dialog => dialog.SetText("Environment file", runtime.EnvironmentFile)));

        Assert.Contains("FROM_FILE=yes", Variables(await WslcCli.InspectAsync(name)));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task WorkingDirectory_IsWhereTheCommandStarts()
    {
        string name = await RunAsync("workdir", InMore(dialog => dialog.SetText("Working directory", "/tmp")));

        Assert.Equal("/tmp", (await WslcCli.InspectAsync(name)).GetProperty("Config").GetProperty("WorkingDir").GetString());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task User_RunsTheCommandAsThatUser()
    {
        string name = await RunAsync("user", InMore(dialog => dialog.SetText("User", "nobody")));

        Assert.Equal("nobody", (await WslcCli.InspectAsync(name)).GetProperty("Config").GetProperty("User").GetString());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task Network_JoinsTheChosenNetwork()
    {
        string name = await RunAsync("network", async dialog =>
        {
            dialog.ExpandMoreOptions();
            await dialog.SelectNetworkAsync(Network);
        });

        Assert.Equal(Network, (await WslcCli.InspectAsync(name)).GetProperty("HostConfig").GetProperty("NetworkMode").GetString());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task Hostname_IsTheContainersHostname()
    {
        string name = await RunAsync("hostname", InMore(dialog => dialog.SetText("Hostname", "e2e-host")));

        Assert.Equal("e2e-host", (await WslcCli.InspectInEngineAsync(name)).GetProperty("Config").GetProperty("Hostname").GetString());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task MemoryLimit_CapsTheContainersMemory()
    {
        string name = await RunAsync("memory", InMore(dialog => dialog.SetText("Memory limit", "256M")));

        Assert.Equal(256L * 1024 * 1024, (await WslcCli.InspectAsync(name)).GetProperty("HostConfig").GetProperty("Memory").GetInt64());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task Cpus_CapsTheContainersCpuShare()
    {
        string name = await RunAsync("cpus", InMore(dialog => dialog.SetText("CPUs", "0.5")));

        Assert.Equal(500_000_000L, (await WslcCli.InspectAsync(name)).GetProperty("HostConfig").GetProperty("NanoCpus").GetInt64());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task Tty_IsAllocatedOnlyWhenTicked()
    {
        string name = await RunAsync("tty", InMore(dialog =>
        {
            dialog.SetChecked("Allocate a terminal (TTY)", true);
            dialog.SetChecked("Keep standard input open", false);
        }));

        JsonElement config = (await WslcCli.InspectInEngineAsync(name)).GetProperty("Config");
        Assert.True(config.GetProperty("Tty").GetBoolean());
        Assert.False(config.GetProperty("OpenStdin").GetBoolean());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task StandardInput_StaysOpenOnlyWhenTicked()
    {
        string name = await RunAsync("stdin", InMore(dialog =>
        {
            dialog.SetChecked("Allocate a terminal (TTY)", false);
            dialog.SetChecked("Keep standard input open", true);
        }));

        JsonElement config = (await WslcCli.InspectInEngineAsync(name)).GetProperty("Config");
        Assert.False(config.GetProperty("Tty").GetBoolean());
        Assert.True(config.GetProperty("OpenStdin").GetBoolean());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task RemoveWhenStopped_RemovesTheContainerOnceItStops()
    {
        string name = await RunAsync("rm", InMore(dialog => dialog.SetChecked("Remove the container when it stops", true)));
        Assert.True(await WslcCli.ContainerExistsAsync(name));

        await WslcCli.RunAsync("stop", name);

        Assert.True(await WaitUntilAsync(async () => !await WslcCli.ContainerExistsAsync(name), TimeSpan.FromSeconds(60)));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task AllGpus_GivesTheContainerTheGpu()
    {
        string name = await RunAsync("gpus", InMore(dialog => dialog.SetChecked("Give the container every GPU", true)), GpuImage);

        JsonElement requests = (await WslcCli.InspectInEngineAsync(name)).GetProperty("HostConfig").GetProperty("DeviceRequests");
        Assert.Equal(JsonValueKind.Array, requests.ValueKind);
        Assert.NotEqual(0, requests.GetArrayLength());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task Run_StartsTheContainerAndShowsIt()
    {
        string name = await RunAsync("run", _ => Task.CompletedTask);

        Assert.True((await WslcCli.InspectAsync(name)).GetProperty("State").GetProperty("Running").GetBoolean());
        Assert.NotNull(await WaitForAsync(() => Find(app.Window, name)));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task Create_CreatesTheContainerWithoutStartingIt()
    {
        string name = await RunAsync("create", _ => Task.CompletedTask, button: "Create");

        Assert.Equal("created", (await WslcCli.InspectAsync(name)).GetProperty("State").GetProperty("Status").GetString());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task ShellImage_TicksTerminalAndStandardInput()
    {
        RunDialog dialog = await RunDialog.OpenAsync(app, ShellImage);
        dialog.ExpandMoreOptions();

        Assert.True(dialog.IsChecked("Allocate a terminal (TTY)"));
        Assert.True(dialog.IsChecked("Keep standard input open"));
        dialog.Click("Cancel");
        Assert.True(await dialog.WaitClosedAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task ServerImage_ListsItsExposedPortsAndLeavesTheTerminalOff()
    {
        RunDialog dialog = await RunDialog.OpenAsync(app, ServerImage);
        dialog.ExpandMoreOptions();

        Assert.Equal("80/tcp", dialog.GetText("Container port, e.g. 80/tcp"));
        Assert.Equal("", dialog.GetText("Host port"));
        Assert.False(dialog.IsChecked("Allocate a terminal (TTY)"));
        Assert.False(dialog.IsChecked("Keep standard input open"));
        dialog.Click("Cancel");
        Assert.True(await dialog.WaitClosedAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task UnclosedQuote_KeepsTheDialogOpenWithTheReason()
    {
        string name = NewName("quote");
        RunDialog dialog = await RunDialog.OpenAsync(app, ShellImage);
        dialog.SetText("Container name", name);
        dialog.ExpandMoreOptions();
        dialog.SetText("Command", "echo \"unclosed");

        dialog.Click("Run");

        Assert.NotNull(await dialog.WaitForErrorAsync("The command cannot be read"));
        Assert.True(dialog.IsOpen);
        await CancelAsync(dialog);
        Assert.False(await WslcCli.ContainerExistsAsync(name));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task RefusedOptions_KeepTheDialogOpenWithWslcsError()
    {
        string name = NewName("refused");
        _containers.Add(name);
        RunDialog dialog = await RunDialog.OpenAsync(app, ShellImage);
        dialog.SetText("Container name", name);
        dialog.ExpandMoreOptions();
        dialog.SetText("Memory limit", "lots");
        Assert.False(dialog.HasError);

        dialog.Click("Run");

        Assert.True(await WaitForAsync(() => dialog.HasError, TimeSpan.FromSeconds(60)));
        Assert.True(dialog.IsOpen);
        await CancelAsync(dialog);
        Assert.False(await WslcCli.ContainerExistsAsync(name));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(RuntimeFixture.Enabled), SkipType = typeof(RuntimeFixture))]
    public async Task Cancel_CreatesNothing()
    {
        string name = NewName("cancel");
        RunDialog dialog = await RunDialog.OpenAsync(app, ShellImage);
        dialog.SetText("Container name", name);

        await CancelAsync(dialog);

        Assert.False(await WslcCli.ContainerExistsAsync(name));
    }

    /// <summary>
    ///     Runs a container through the dialog with the option under test filled in, and returns its name.
    /// </summary>
    private async Task<string> RunAsync(string feature, Func<RunDialog, Task> fill, string image = ShellImage, string command = KeepRunning,
        string button = "Run")
    {
        string name = NewName(feature);
        _containers.Add(name);

        RunDialog dialog = await RunDialog.OpenAsync(app, image);
        dialog.SetText("Container name", name);
        if (command.Length > 0)
        {
            dialog.ExpandMoreOptions();
            dialog.SetText("Command", command);
        }

        await fill(dialog);
        dialog.Click(button);

        Assert.True(await dialog.WaitClosedAsync(), "The dialog stayed open; wslc refused the options or never answered.");
        Assert.True(await WslcCli.ContainerExistsAsync(name), $"wslc has no container named {name}.");
        Assert.False(app.CrashLogChanged(), app.CrashLogTail());
        return name;
    }

    /// <summary>
    ///     Fills in an option that sits under More options.
    /// </summary>
    private static Func<RunDialog, Task> InMore(Action<RunDialog> fill) => dialog =>
    {
        dialog.ExpandMoreOptions();
        fill(dialog);
        return Task.CompletedTask;
    };

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                return false;
            }

            await Task.Delay(500);
        }

        return true;
    }

    private static async Task CancelAsync(RunDialog dialog)
    {
        dialog.Click("Cancel");
        Assert.True(await dialog.WaitClosedAsync());
    }

    private static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString()!)];

    private static string[] Variables(JsonElement container) => Strings(container.GetProperty("Config").GetProperty("Env"));

    private static Mount[] Mounts(JsonElement container) =>
    [
        .. container.GetProperty("Mounts").EnumerateArray().Select(mount => new Mount(
            mount.GetProperty("Type").GetString()!,
            mount.GetProperty("Name").GetString()!,
            mount.GetProperty("Source").GetString()!,
            mount.GetProperty("Destination").GetString()!))
    ];

    private sealed record Mount(string Type, string Name, string Source, string Destination);
}
