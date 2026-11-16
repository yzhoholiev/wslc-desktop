using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class VolumeSizesTests
{
    [Fact]
    public void VolumeSizesArguments_NameTheDefaultSessionAndAskOnlyForVolumes() =>
        Assert.Equal(
            [
                "--session", Wslc.DefaultSessionName, "system", "session", "run", "curl", "-sS", "--fail", "--max-time", "60",
                "--unix-socket", "/var/run/docker.sock", "http://localhost/v1.42/system/df?type=volume"
            ],
            WslcCli.VolumeSizesArguments);

    [Fact]
    public void ParseVolumeSizes_ReadsBytesPerVolume()
    {
        IReadOnlyDictionary<string, long> sizes = DockerEngine.ParseVolumeSizes(
            """{ "LayersSize": 0, "Images": null, "Volumes": [{ "Name": "data", "UsageData": { "RefCount": 1, "Size": 316416215 } }, { "Name": "empty", "UsageData": { "RefCount": 0, "Size": 0 } }], "BuildCache": null }""");

        Assert.Equal(2, sizes.Count);
        Assert.Equal(316416215, sizes["data"]);
        Assert.Equal(0, sizes["empty"]);
    }

    [Fact]
    public void ParseVolumeSizes_LeavesOutVolumesTheEngineCouldNotMeasure()
    {
        IReadOnlyDictionary<string, long> sizes = DockerEngine.ParseVolumeSizes(
            """{ "Volumes": [{ "Name": "remote", "UsageData": { "RefCount": 1, "Size": -1 } }, { "Name": "bare" }, { "Name": "nulled", "UsageData": null }] }""");

        Assert.Empty(sizes);
    }

    [Fact]
    public void ParseVolumeSizes_ReturnsNothingWithoutVolumes() =>
        Assert.Empty(DockerEngine.ParseVolumeSizes("""{ "Volumes": null }"""));

    [Fact]
    public void ParseVolumeSizes_SkipsNamelessVolumesAndKeepsOneSizePerName()
    {
        IReadOnlyDictionary<string, long> sizes = DockerEngine.ParseVolumeSizes(
            """{ "Volumes": [{ "Name": null, "UsageData": { "Size": 1 } }, { "UsageData": { "Size": 2 } }, { "Name": "data", "UsageData": { "Size": 3 } }, { "Name": "data", "UsageData": { "Size": 4 } }] }""");

        Assert.Equal(4, Assert.Single(sizes).Value);
    }

    public static TheoryData<string> OutputsThatAreNotJson => new("", "curl: (7) Couldn't connect to server");

    [Theory]
    [MemberData(nameof(OutputsThatAreNotJson))]
    public void ParseVolumeSizes_ThrowsCliExceptionForAResponseThatIsNotJson(string output) =>
        Assert.ThrowsAny<CliException>(() => DockerEngine.ParseVolumeSizes(output));
}
