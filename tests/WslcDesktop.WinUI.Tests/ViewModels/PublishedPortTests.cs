using WslcDesktop.ViewModels;

namespace WslcDesktop.Tests.ViewModels;

public sealed class PublishedPortTests
{
    [Fact]
    public void ParseList_ReadsTheCliNotation()
    {
        PublishedPort[] ports = PublishedPort.ParseList("127.0.0.1:8080->80/tcp, 0.0.0.0:15353->53/udp, 9000/tcp, [::]:443->8443/tcp, 8081->81");

        Assert.Equal(
            [
                new PublishedPort("127.0.0.1", 8080, 80, "tcp"),
                new PublishedPort("0.0.0.0", 15353, 53, "udp"),
                new PublishedPort("", null, 9000, "tcp"),
                new PublishedPort("[::]", 443, 8443, "tcp"),
                new PublishedPort("", 8081, 81, "tcp")
            ],
            ports);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage, :->x/tcp")]
    public void ParseList_SkipsWhatItCannotRead(string ports) => Assert.Empty(PublishedPort.ParseList(ports));

    [Theory]
    [InlineData("127.0.0.1:8080->80/tcp", "http://127.0.0.1:8080/")]
    [InlineData("0.0.0.0:8080->80/tcp", "http://localhost:8080/")]
    [InlineData("[::]:8080->80/tcp", "http://localhost:8080/")]
    [InlineData("8080->80/tcp", "http://localhost:8080/")]
    [InlineData("::1:8080->80/tcp", "http://[::1]:8080/")]
    [InlineData("[::1]:8080->80/tcp", "http://[::1]:8080/")]
    [InlineData("127.0.0.1:15353->53/udp", null)]
    [InlineData("9000/tcp", null)]
    public void Url_OpensPublishedTcpPortsOnTheBoundAddress(string ports, string? expected) =>
        Assert.Equal(expected, Assert.Single(PublishedPort.ParseList(ports)).Url?.AbsoluteUri);

    [Fact]
    public void LabelAndDescription_ReadNaturally()
    {
        var published = new PublishedPort("127.0.0.1", 8080, 80, "tcp");
        var unpublished = new PublishedPort("", null, 9000, "tcp");

        Assert.Equal("8080 → 80", published.Label);
        Assert.Equal("127.0.0.1:8080 → 80/tcp", published.Description);
        Assert.Equal("9000/tcp", unpublished.Label);
        Assert.Equal("9000/tcp (not published)", unpublished.Description);
    }
}
