using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class WslcSettingsParserTests
{
    private const long GiB = 1L << 30;

    private static readonly Dictionary<string, (string Yaml, WslcSettings Expected)> Files = new()
    {
        ["no file"] = ("", new WslcSettings()),
        ["as wslc settings creates it"] = (
            """
            session:
              # cpuCount: default
              # memorySize: default
            # credentialStore: default
            """,
            new WslcSettings()),
        ["every setting"] = (
            """
            session:
              cpuCount: 4
              memorySize: 8GB
              maxStorageSize: 500GB
              storagePath: D:\data
              defaultBindingAddress: 0.0.0.0
              hostLoopback: none
              idleTimeout: 60
            credentialStore: file
            """,
            new WslcSettings(4, 8 * GiB, 500 * GiB, @"D:\data", "0.0.0.0", "none", 60, "file")),
        ["default, quotes, comments and CRLF"] = (
            "session:\r\n  cpuCount: default\r\n  memorySize: \"16gb\" # half the host\r\n  storagePath: 'D:\\wslc data'\r\n",
            new WslcSettings(MemorySize: 16 * GiB, StoragePath: @"D:\wslc data")),
        ["size units and bytes"] = (
            "session:\n  memorySize: 2048M\n  maxStorageSize: 1t\n",
            new WslcSettings(MemorySize: 2 * GiB, MaxStorageSize: 1024 * GiB)),
        ["invalid values use the defaults"] = (
            "session:\n  cpuCount: 0\n  memorySize: 512KB\n  maxStorageSize: lots\n  idleTimeout: -5\ncredentialStore: keychain\n",
            new WslcSettings()),
        ["keys outside session are not session settings"] = (
            "cpuCount: 4\nother:\n  memorySize: 8GB\nsession:\n  idleTimeout: 10\n",
            new WslcSettings(IdleTimeoutSeconds: 10))
    };

    public static TheoryData<string> FileNames { get; } = new(Files.Keys);

    [Theory]
    [MemberData(nameof(FileNames))]
    public void Parse_ReadsTheSettingsWslcUses(string caseName)
    {
        (string yaml, WslcSettings expected) = Files[caseName];

        Assert.Equal(expected, WslcSettingsParser.Parse(yaml));
    }

    [Fact]
    public void Describe_ShowsTheDefaultsForThisHost()
    {
        KeyValueEntry[] rows = WslcSettingsParser.Describe(new WslcSettings(), 12, 64 * GiB);

        Assert.Equal(
            [
                new KeyValueEntry("CPUs", "12 (default: all logical processors)"),
                new KeyValueEntry("Memory", "32 GiB (default: half of 64 GiB)"),
                new KeyValueEntry("Disk size limit", "1 TiB (default)"),
                new KeyValueEntry("Storage location", "%LOCALAPPDATA% (default)"),
                new KeyValueEntry("Port binding address", "127.0.0.1 (default)"),
                new KeyValueEntry("Windows host name", "host.wslc.internal (default)"),
                new KeyValueEntry("Idle shutdown", "30 s (default)"),
                new KeyValueEntry("Registry credentials", "Windows Credential Manager (default)")
            ],
            rows);
    }

    [Fact]
    public void Describe_ShowsConfiguredValuesAsSet()
    {
        var settings = new WslcSettings(4, 8 * GiB, 500 * GiB, @"D:\data", "0.0.0.0", "none", 60, "file");

        KeyValueEntry[] rows = WslcSettingsParser.Describe(settings, 12, 64 * GiB);

        Assert.Equal(
            ["4", "8 GiB", "500 GiB", @"D:\data", "0.0.0.0", "none", "60 s", "Encrypted file"],
            rows.Select(row => row.Value));
    }
}
