using System.Text.Json.Serialization.Metadata;
using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Reads the runtime by running one wslc command per query.
/// </summary>
internal sealed class CliRuntimeReader : IRuntimeReader
{
    private static readonly WslcJsonContext Json = WslcJsonContext.Default;

    public Task<List<ContainerDto>> ListContainersAsync(CancellationToken ct) =>
        ListAsync(["list", "--all", "--no-trunc", "--format", "json"], Json.ContainerDto, Json.ListContainerDto, ct);

    /// <summary>
    ///     wslc stats reports every running container itself.
    /// </summary>
    public Task<List<ContainerStatsDto>> GetStatsAsync(IReadOnlyList<ContainerDto> containers, CancellationToken ct) =>
        ListAsync(["stats", "--no-trunc", "--format", "json"], Json.ContainerStatsDto, Json.ListContainerStatsDto, ct);

    public Task<List<ImageDto>> ListImagesAsync(CancellationToken ct) => ListAsync(["images", "--format", "json"], Json.ImageDto, Json.ListImageDto, ct);

    public Task<List<VolumeDto>> ListVolumesAsync(CancellationToken ct) =>
        ListAsync(["volume", "list", "--format", "json"], Json.VolumeDto, Json.ListVolumeDto, ct);

    public Task<List<NetworkDto>> ListNetworksAsync(CancellationToken ct) =>
        ListAsync(["network", "list", "--no-trunc", "--format", "json"], Json.NetworkDto, Json.ListNetworkDto, ct);

    public async Task<ImageConfigDto> GetImageConfigAsync(string image, CancellationToken ct) =>
        ParseImageConfig(await WslcCli.RunAsync(WslcCli.QueryTimeout, QueryArguments(["image", "inspect", image]), ct));

    public Task<string> ReadVmStatusAsync(CancellationToken ct) => RunInSessionAsync(SessionCommands.VmStatus, ct);

    public Task<string> ReadStorageUsageAsync(CancellationToken ct) => RunInSessionAsync(SessionCommands.StorageUsage, ct);

    internal static ImageConfigDto ParseImageConfig(string inspectOutput) =>
        WslcJsonContext.ParseList(inspectOutput, Json.ImageInspectDto, Json.ListImageInspectDto).FirstOrDefault()?.Config ?? new ImageConfigDto();

    /// <summary>
    ///     The wslc arguments for a query on the default session, named so that wslc fails instead of creating it.
    /// </summary>
    internal static string[] QueryArguments(string[] query) => ["--session", Wslc.DefaultSessionName, .. query];

    private static async Task<List<T>> ListAsync<T>(string[] query, JsonTypeInfo<T> itemInfo, JsonTypeInfo<List<T>> listInfo, CancellationToken ct) =>
        WslcJsonContext.ParseList(await WslcCli.RunAsync(WslcCli.QueryTimeout, QueryArguments(query), ct), itemInfo, listInfo);

    /// <summary>
    ///     The wslc arguments that run a command in the default session, named so that wslc cannot create it and boot a VM the user had stopped.
    /// </summary>
    internal static string[] SessionArguments(string[] command) => WslcCli.SessionRunArguments(Wslc.DefaultSessionName, command);

    private static Task<string> RunInSessionAsync(string[] command, CancellationToken ct) =>
        WslcCli.RunAsync(WslcCli.QueryTimeout, SessionArguments(command), ct);
}
