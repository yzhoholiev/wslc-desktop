using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class DockerEngineTests
{
    private const string ContainersJson = """
                                          [
                                            {
                                              "Id": "web0123456789", "Names": ["/demo-web"], "Image": "nginx:alpine", "ImageID": "sha256:111",
                                              "Command": "nginx", "Created": 1760000000, "State": "running", "Status": "Up 5 minutes",
                                              "Ports": [{ "IP": "0.0.0.0", "PrivatePort": 80, "PublicPort": 32768, "Type": "tcp" }],
                                              "Labels": { "com.microsoft.wsl.container.metadata": "{\"V1\":{\"Ports\":[{\"BindingAddress\":\"127.0.0.1\",\"ContainerPort\":80,\"HostPort\":8080,\"Protocol\":6},{\"BindingAddress\":\"127.0.0.1\",\"ContainerPort\":53,\"HostPort\":15353,\"Protocol\":17}]}}" },
                                              "NetworkSettings": { "Networks": { "demo-net": {}, "bridge": {} } },
                                              "Mounts": [{ "Type": "volume", "Name": "demo-pgdata" }, { "Type": "bind", "Name": "" }]
                                            },
                                            {
                                              "Id": "plain0123456789", "Names": ["/plain"], "Image": "busybox", "ImageID": "sha256:222",
                                              "Created": 1760000000, "State": "exited", "Status": "Exited (0)",
                                              "Ports": [{ "PrivatePort": 8080, "Type": "tcp" }, { "IP": "0.0.0.0", "PrivatePort": 53, "PublicPort": 5353, "Type": "udp" }]
                                            }
                                          ]
                                          """;

    [Fact]
    public async Task ListContainersAsync_MapsEngineContainers()
    {
        var engine = new DockerEngine(new FakeShell { ["/containers/json?all=1"] = ContainersJson });

        List<ContainerDto> containers = await engine.ListContainersAsync(TestContext.Current.CancellationToken);

        ContainerDto web = containers[0];
        Assert.Equal("demo-web", web.Names);
        Assert.Equal("sha256:111", web.ImageId);
        Assert.Equal("127.0.0.1:8080->80/tcp, 127.0.0.1:15353->53/udp", web.Ports);
        Assert.Equal("demo-pgdata", web.Mounts);
        Assert.Equal("demo-net,bridge", web.Networks);
        Assert.True(web.IsRunning);

        ContainerDto plain = containers[1];
        Assert.Equal("8080/tcp, 0.0.0.0:5353->53/udp", plain.Ports);
        Assert.Equal("", plain.Mounts);
        Assert.False(plain.IsActive);
    }

    [Fact]
    public async Task ListImagesAsync_ExpandsTagsAndCountsContainersByImageId()
    {
        var engine = new DockerEngine(new FakeShell
        {
            ["/images/json"] = """
                               [
                                 { "Id": "sha256:111", "RepoTags": ["nginx:alpine", "nginx:latest"], "Created": 1760000000, "Size": 69100000 },
                                 { "Id": "sha256:333", "RepoTags": null, "Created": 1760000000, "Size": 1000 }
                               ]
                               """,
            ["/containers/json?all=1"] = ContainersJson
        });

        List<ImageDto> images = await engine.ListImagesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["nginx:alpine", "nginx:latest", "<none>:<none>"], images.Select(image => $"{image.Repository}:{image.Tag}"));
        Assert.Equal(["1", "1", "0"], images.Select(image => image.Containers));
        Assert.Equal("65.9 MiB", images[0].Size);
    }

    [Fact]
    public async Task ListVolumesAsync_FlattensLabels()
    {
        var engine = new DockerEngine(new FakeShell
        {
            ["/volumes"] =
                """{ "Volumes": [{ "Name": "a34c", "Driver": "local", "Mountpoint": "/var/lib/docker/volumes/a34c/_data", "Scope": "local", "Labels": { "com.docker.volume.anonymous": "" } }] }"""
        });

        VolumeDto volume = Assert.Single(await engine.ListVolumesAsync(TestContext.Current.CancellationToken));

        Assert.Equal("com.docker.volume.anonymous=", volume.Labels);
        Assert.True(volume.IsAnonymous);
        Assert.Equal("/var/lib/docker/volumes/a34c/_data", volume.Mountpoint);
    }

    [Fact]
    public async Task GetImageConfigAsync_ReadsTheImagesDefaults()
    {
        var shell = new FakeShell
        {
            ["/images/nginx:alpine/json"] =
                """{ "Id": "sha256:111", "Config": { "Cmd": ["nginx", "-g", "daemon off;"], "ExposedPorts": { "80/tcp": {} }, "WorkingDir": "/", "Volumes": null } }"""
        };

        ImageConfigDto config = await new DockerEngine(shell).GetImageConfigAsync("nginx:alpine", TestContext.Current.CancellationToken);

        Assert.Equal(["nginx", "-g", "daemon off;"], config.Cmd!);
        Assert.Equal(["80/tcp"], config.ExposedPorts!.Keys);
        Assert.Equal("/", config.WorkingDir);
        Assert.Null(config.Volumes);
    }

    [Fact]
    public async Task GetImageConfigAsync_ReportsAMissingImageWithoutGivingUpOnTheEngine()
    {
        var engine = new DockerEngine(new FakeShell { ["/images/gone:1/json"] = new ShellResult(22, "curl: (22) The requested URL returned error: 404") });

        CliException error = await Assert.ThrowsAsync<CliException>(() => engine.GetImageConfigAsync("gone:1", TestContext.Current.CancellationToken));

        Assert.IsNotType<EngineUnavailableException>(error);
        Assert.Contains("gone:1", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nginx'; rm -rf / #")]
    public async Task GetImageConfigAsync_RefusesReferencesThatAreNotSafeInAShellCommand(string image)
    {
        var shell = new FakeShell();

        CliException error =
            await Assert.ThrowsAsync<CliException>(() => new DockerEngine(shell).GetImageConfigAsync(image, TestContext.Current.CancellationToken));

        Assert.IsNotType<EngineUnavailableException>(error);
        Assert.Empty(shell.Commands);
    }

    [Theory]
    [InlineData("registry.example.com:5000/team/app:1.2-rc_1")]
    public async Task GetImageConfigAsync_AcceptsAValidReference(string image)
    {
        var shell = new FakeShell { [$"/images/{image}/json"] = """{ "Config": {} }""" };

        await new DockerEngine(shell).GetImageConfigAsync(image, TestContext.Current.CancellationToken);

        Assert.Contains($"'/images/{image}/json'", Assert.Single(shell.Commands), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListNetworksAsync_MapsFlagsAndCreationTime()
    {
        var engine = new DockerEngine(new FakeShell
        {
            ["/networks"] =
                """[{ "Id": "n1", "Name": "demo-net", "Driver": "bridge", "Scope": "local", "Internal": true, "EnableIPv6": false, "Created": "2026-10-19T13:27:24.0023Z" }]"""
        });

        NetworkDto network = Assert.Single(await engine.ListNetworksAsync(TestContext.Current.CancellationToken));

        Assert.Equal("true", network.Internal);
        Assert.Equal("false", network.IPv6);
        Assert.Equal("2026-10-19 13:27:24.0023Z", network.CreatedAt);
    }

    [Fact]
    public async Task GetStatsAsync_ComputesCpuFromConsecutiveSamplesAndExcludesPageCache()
    {
        var shell = new FakeShell { ["/stats"] = StatsOutput(1000, 10_000) };
        var engine = new DockerEngine(shell);
        CancellationToken ct = TestContext.Current.CancellationToken;

        ContainerDto[] web = [new() { Id = "web0123456789", State = "running" }, new() { Id = "stopped", State = "exited" }];

        ContainerStatsDto first = Assert.Single(await engine.GetStatsAsync(web, ct));
        shell["/stats"] = StatsOutput(1500, 11_000);
        ContainerStatsDto second = Assert.Single(await engine.GetStatsAsync(web, ct));

        Assert.Equal("web0123456789", first.Id);
        Assert.Equal("0.00%", first.CpuPerc);
        // 500 of 1000 system ticks on 4 CPUs, as the Docker CLI scales it.
        Assert.Equal("200.00%", second.CpuPerc);
        Assert.Equal("8 MiB / 100 MiB", second.MemUsage);
        Assert.Equal("8.00%", second.MemPerc);
        Assert.Equal("1000 B / 2 KiB", second.NetIO);
        Assert.Equal("4 KiB / 8 KiB", second.BlockIO);
        Assert.Equal(3, second.Pids);
    }

    [Fact]
    public async Task GetStatsAsync_SkipsTheShellWithoutRunningContainers()
    {
        var shell = new FakeShell();

        Assert.Empty(
            await new DockerEngine(shell).GetStatsAsync([new ContainerDto { Id = "stopped", State = "exited" }], TestContext.Current.CancellationToken));

        Assert.Empty(shell.Commands);
    }

    [Fact]
    public async Task Requests_FailAsEngineUnavailableWhenCurlFails()
    {
        var engine = new DockerEngine(new FakeShell { ["/networks"] = new ShellResult(7, "curl: (7) Couldn't connect to server") });

        EngineUnavailableException error =
            await Assert.ThrowsAsync<EngineUnavailableException>(() => engine.ListNetworksAsync(TestContext.Current.CancellationToken));

        Assert.Contains("(7)", error.Message);
    }

    [Fact]
    public async Task Requests_FailAsEngineUnavailableOnUnexpectedJson()
    {
        var engine = new DockerEngine(new FakeShell { ["/networks"] = "<html>not json</html>" });

        await Assert.ThrowsAsync<EngineUnavailableException>(() => engine.ListNetworksAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SessionCommands_ReturnOutputOrThrowWithIt()
    {
        var shell = new FakeShell { ["cat /proc/loadavg"] = "0.10 0.20 0.30", ["df -B1"] = new ShellResult(1, "df: not found") };
        var engine = new DockerEngine(shell);
        CancellationToken ct = TestContext.Current.CancellationToken;

        Assert.Equal("0.10 0.20 0.30", await engine.ReadVmStatusAsync(ct));
        CliException error = await Assert.ThrowsAsync<CliException>(() => engine.ReadStorageUsageAsync(ct));
        Assert.Equal("df: not found", error.Message);
        Assert.Equal(["cat /proc/loadavg /proc/uptime /proc/meminfo /proc/stat", "df -B1 /var/lib/docker"], shell.Commands);
    }

    [Fact]
    public void Connection_FollowsTheShell()
    {
        var shell = new FakeShell();
        var engine = new DockerEngine(shell);

        Assert.True(engine.IsConnected);

        engine.Dispose();
        Assert.False(engine.IsConnected);
    }

    [Fact]
    public async Task Requests_QuoteThePathForTheShell()
    {
        var shell = new FakeShell { ["/containers/json?all=1"] = "[]" };

        await new DockerEngine(shell).ListContainersAsync(TestContext.Current.CancellationToken);

        Assert.EndsWith("'/containers/json?all=1' 2>&1", Assert.Single(shell.Commands));
    }

    private static string StatsOutput(ulong totalUsage, ulong systemUsage) => $$$"""
                                                                                 @id web0123456789
                                                                                 {"cpu_stats":{"cpu_usage":{"total_usage":{{{totalUsage}}}},"system_cpu_usage":{{{systemUsage}}},"online_cpus":4},"memory_stats":{"usage":10485760,"limit":104857600,"stats":{"inactive_file":2097152}},"networks":{"eth0":{"rx_bytes":1000,"tx_bytes":2000}},"blkio_stats":{"io_service_bytes_recursive":[{"op":"read","value":4096},{"op":"Write","value":8192}]},"pids_stats":{"current":3}}

                                                                                 """;
}
