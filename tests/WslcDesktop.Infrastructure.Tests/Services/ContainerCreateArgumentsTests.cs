using System.Reflection;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class ContainerCreateArgumentsTests
{
    private const string Image = "registry.example.com/team/app:1.2";

    /// <summary>
    ///     Each case's options and the arguments expected between "create" and the image, or after the image for the command.
    /// </summary>
    private static readonly Dictionary<string, (ContainerRunOptions Options, string[] BeforeImage, string[] AfterImage)> Cases = new()
    {
        ["name"] = (new ContainerRunOptions(Image) { Name = "web" }, ["--name", "web"], []),
        ["name, trimmed"] = (new ContainerRunOptions(Image) { Name = "  web  " }, ["--name", "web"], []),
        ["name, blank"] = (new ContainerRunOptions(Image) { Name = "   " }, [], []),
        ["port"] = (new ContainerRunOptions(Image) { Ports = [new PortMapping("8080", "80/tcp")] }, ["--publish", "8080:80/tcp"], []),
        ["port, without protocol"] = (new ContainerRunOptions(Image) { Ports = [new PortMapping("8080", "80")] }, ["--publish", "8080:80"], []),
        ["port, udp"] = (new ContainerRunOptions(Image) { Ports = [new PortMapping("5353", "53/udp")] }, ["--publish", "5353:53/udp"], []),
        ["port, host address"] =
            (new ContainerRunOptions(Image) { Ports = [new PortMapping("127.0.0.1:8080", "80/tcp")] }, ["--publish", "127.0.0.1:8080:80/tcp"], []),
        ["port, blank container port"] = (new ContainerRunOptions(Image) { Ports = [new PortMapping("8080", " ")] }, [], []),
        ["port, trimmed"] = (new ContainerRunOptions(Image) { Ports = [new PortMapping(" 8080 ", " 80/tcp ")] }, ["--publish", "8080:80/tcp"], []),
        ["ports, several in order"] = (
            new ContainerRunOptions(Image)
            {
                Ports =
                [
                    new PortMapping("8080", "80/tcp"), new PortMapping("", "81/tcp"), new PortMapping("8443", "443/tcp"), new PortMapping("5353", "53/udp")
                ]
            },
            ["--publish", "8080:80/tcp", "--publish", "8443:443/tcp", "--publish", "5353:53/udp"], []),
        ["publish all"] = (new ContainerRunOptions(Image) { PublishAllPorts = true }, ["--publish-all"], []),
        ["publish all, with ports"] = (
            new ContainerRunOptions(Image) { Ports = [new PortMapping("8080", "80/tcp")], PublishAllPorts = true },
            ["--publish", "8080:80/tcp", "--publish-all"], []),
        ["volume, host folder"] =
            (new ContainerRunOptions(Image) { Volumes = [new VolumeMapping(@"D:\data", "/data")] }, ["--volume", @"D:\data:/data"], []),
        ["volume, folder with spaces"] = (
            new ContainerRunOptions(Image) { Volumes = [new VolumeMapping(@"C:\My Projects\site", "/usr/share/nginx/html")] },
            ["--volume", @"C:\My Projects\site:/usr/share/nginx/html"], []),
        ["volume, named"] = (
            new ContainerRunOptions(Image) { Volumes = [new VolumeMapping("pgdata", "/var/lib/postgresql/data")] },
            ["--volume", "pgdata:/var/lib/postgresql/data"], []),
        ["volume, blank target"] = (new ContainerRunOptions(Image) { Volumes = [new VolumeMapping("cache", "")] }, [], []),
        ["volumes, several in order"] = (
            new ContainerRunOptions(Image)
            {
                Volumes = [new VolumeMapping(@"D:\config", "/etc/app"), new VolumeMapping("", "/skipped"), new VolumeMapping("cache", "/var/cache/app")]
            },
            ["--volume", @"D:\config:/etc/app", "--volume", "cache:/var/cache/app"], []),
        ["variable"] = (new ContainerRunOptions(Image) { Environment = [new KeyValueEntry("MODE", "production")] }, ["--env", "MODE=production"], []),
        ["variable, empty value"] = (new ContainerRunOptions(Image) { Environment = [new KeyValueEntry("DEBUG", "")] }, ["--env", "DEBUG="], []),
        ["variable, value kept as typed"] = (
            new ContainerRunOptions(Image) { Environment = [new KeyValueEntry(" GREETING ", "  hello world  ")] },
            ["--env", "GREETING=  hello world  "], []),
        ["variable, value with equals signs"] = (
            new ContainerRunOptions(Image) { Environment = [new KeyValueEntry("DSN", "host=db;user=app")] },
            ["--env", "DSN=host=db;user=app"], []),
        ["variables, several in order"] = (
            new ContainerRunOptions(Image)
            {
                Environment = [new KeyValueEntry("A", "1"), new KeyValueEntry(" ", "orphan"), new KeyValueEntry("B", "2"), new KeyValueEntry("C", "")]
            },
            ["--env", "A=1", "--env", "B=2", "--env", "C="], []),
        ["environment file"] =
            (new ContainerRunOptions(Image) { EnvironmentFile = @"C:\env\app.env" }, ["--env-file", @"C:\env\app.env"], []),
        ["entrypoint"] = (new ContainerRunOptions(Image) { Entrypoint = "/bin/sh" }, ["--entrypoint", "/bin/sh"], []),
        ["working directory"] = (new ContainerRunOptions(Image) { WorkingDirectory = "/srv/app" }, ["--workdir", "/srv/app"], []),
        ["user, by name"] = (new ContainerRunOptions(Image) { User = "nginx" }, ["--user", "nginx"], []),
        ["user, by uid and gid"] = (new ContainerRunOptions(Image) { User = "1000:1000" }, ["--user", "1000:1000"], []),
        ["network"] = (new ContainerRunOptions(Image) { Network = "backend" }, ["--network", "backend"], []),
        ["hostname"] = (new ContainerRunOptions(Image) { Hostname = "web1" }, ["--hostname", "web1"], []),
        ["memory"] = (new ContainerRunOptions(Image) { Memory = "512M" }, ["--memory", "512M"], []),
        ["cpus"] = (new ContainerRunOptions(Image) { Cpus = "0.5" }, ["--cpus", "0.5"], []),
        ["all gpus"] = (new ContainerRunOptions(Image) { AllGpus = true }, ["--gpus", "all"], []),
        ["tty"] = (new ContainerRunOptions(Image) { Tty = true }, ["--tty"], []),
        ["interactive"] = (new ContainerRunOptions(Image) { Interactive = true }, ["--interactive"], []),
        ["tty and interactive"] = (new ContainerRunOptions(Image) { Tty = true, Interactive = true }, ["--tty", "--interactive"], []),
        ["remove when stopped"] = (new ContainerRunOptions(Image) { RemoveWhenStopped = true }, ["--rm"], []),
        ["command"] = (new ContainerRunOptions(Image) { Command = "redis-server" }, [], ["redis-server"]),
        ["command, with arguments"] =
            (new ContainerRunOptions(Image) { Command = "redis-server --appendonly yes" }, [], ["redis-server", "--appendonly", "yes"]),
        ["command, single quotes"] = (new ContainerRunOptions(Image) { Command = "sh -c 'echo hi; sleep 5'" }, [], ["sh", "-c", "echo hi; sleep 5"]),
        ["command, double quotes"] = (new ContainerRunOptions(Image) { Command = "echo \"a  b\" c" }, [], ["echo", "a  b", "c"]),
        ["command, blank"] = (new ContainerRunOptions(Image) { Command = "   " }, [], [])
    };

    public static TheoryData<string> CaseNames { get; } = new(Cases.Keys);

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void CreateArguments_MapsEachOption(string caseName)
    {
        (ContainerRunOptions? options, string[]? beforeImage, string[]? afterImage) = Cases[caseName];

        Assert.Equal(["create", .. beforeImage, Image, .. afterImage], WslcCli.CreateArguments(options, false));
        Assert.Equal(["run", "--detach", .. beforeImage, Image, .. afterImage], WslcCli.CreateArguments(options, true));
    }

    [Fact]
    public void CreateArguments_MapsEveryOptionProperty()
    {
        string[] baseline = WslcCli.CreateArguments(new ContainerRunOptions(Image), false);
        IEnumerable<PropertyInfo> properties = typeof(ContainerRunOptions).GetProperties()
            .Where(property => property.CanWrite && property.Name != nameof(ContainerRunOptions.Image));

        Assert.All(properties, property =>
        {
            var options = new ContainerRunOptions(Image);
            property.SetValue(options, SampleValue(property.PropertyType));
            Assert.NotEqual(baseline, WslcCli.CreateArguments(options, false));
        });
    }

    private static object SampleValue(Type type) =>
        type == typeof(string) ? "sample"
        : type == typeof(bool) ? true
        : type == typeof(PortMapping[]) ? new[] { new PortMapping("8080", "80/tcp") }
        : type == typeof(VolumeMapping[]) ? new[] { new VolumeMapping("data", "/data") }
        : type == typeof(KeyValueEntry[]) ? new[] { new KeyValueEntry("KEY", "value") }
        : throw new InvalidOperationException($"Add a sample value for options of type {type.Name}.");

    [Fact]
    public void CreateArguments_PassesEveryOptionBeforeTheImageAndTheCommandAfterIt()
    {
        var options = new ContainerRunOptions("nginx:alpine")
        {
            Name = " web ",
            Ports = [new PortMapping("8080", "80/tcp")],
            PublishAllPorts = true,
            Volumes = [new VolumeMapping(@"C:\data", "/usr/share/nginx/html")],
            Environment = [new KeyValueEntry("MODE", "dev mode"), new KeyValueEntry("EMPTY", "")],
            EnvironmentFile = @"C:\app.env",
            Command = "nginx -g 'daemon off;'",
            Entrypoint = "/docker-entrypoint.sh",
            WorkingDirectory = "/srv",
            User = "1000:1000",
            Network = "backend",
            Hostname = "web1",
            Memory = "512M",
            Cpus = "1.5",
            Tty = true,
            Interactive = true,
            RemoveWhenStopped = true,
            AllGpus = true
        };

        Assert.Equal(
            [
                "run", "--detach",
                "--name", "web",
                "--publish", "8080:80/tcp",
                "--publish-all",
                "--volume", @"C:\data:/usr/share/nginx/html",
                "--env", "MODE=dev mode",
                "--env", "EMPTY=",
                "--env-file", @"C:\app.env",
                "--entrypoint", "/docker-entrypoint.sh",
                "--workdir", "/srv",
                "--user", "1000:1000",
                "--network", "backend",
                "--hostname", "web1",
                "--memory", "512M",
                "--cpus", "1.5",
                "--gpus", "all",
                "--tty",
                "--interactive",
                "--rm",
                "nginx:alpine",
                "nginx", "-g", "daemon off;"
            ],
            WslcCli.CreateArguments(options, true));
    }

    [Fact]
    public void CreateArguments_RejectsACommandWithAnUnclosedQuote() =>
        Assert.Throws<FormatException>(() => WslcCli.CreateArguments(new ContainerRunOptions("alpine") { Command = "sh -c 'echo" }, true));
}
