using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class InventoryReaderTests
{
    [Fact]
    public void ParseDiskUsage_ReadsTheDfLine()
    {
        DiskUsage? usage = InventoryReader.ParseDiskUsage("""
                                                          Filesystem        1B-blocks       Used     Available Use% Mounted on
                                                          /dev/sdc      1081101176832 4617236480 1021412900864   1% /var/lib/docker
                                                          """);

        Assert.NotNull(usage);
        Assert.Equal(1_081_101_176_832L, usage.Total);
        Assert.Equal(4_617_236_480L, usage.Used);
    }

    [Theory]
    [InlineData("")]
    [InlineData("df: /var/lib/docker: No such file or directory")]
    public void ParseDiskUsage_ReturnsNullWithoutUsage(string output) => Assert.Null(InventoryReader.ParseDiskUsage(output));
}
