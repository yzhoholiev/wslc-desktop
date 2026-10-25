using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Counts the session's images, volumes and networks and reads its container storage, which change far less often than containers.
/// </summary>
internal sealed class InventoryReader(IRuntimeReader reader)
{
    public Inventory? Current { get; private set; }

    public DiskUsage? Storage { get; private set; }

    public async Task RefreshAsync(CancellationToken ct)
    {
        // Listings have a row per tag; an image with several tags is still one image on disk.
        var images = (await reader.ListImagesAsync(ct)).DistinctBy(image => image.Id).ToList();
        List<VolumeDto> volumes = await reader.ListVolumesAsync(ct);
        List<NetworkDto> networks = await reader.ListNetworksAsync(ct);

        try
        {
            Storage = ParseDiskUsage(await reader.ReadStorageUsageAsync(ct));
        }
        catch (CliException ex) when (ex is not SessionEndedException)
        {
            Storage = null;
        }

        var unusedImages = images.Where(image => image.Containers == "0").ToList();
        Current = new Inventory(
            images.Count,
            images.Sum(image => ByteSize.Parse(image.Size)),
            unusedImages.Count,
            volumes.Count,
            volumes.Count(volume => volume.IsAnonymous),
            networks.Count,
            networks.Count(network => !NetworkDto.IsBuiltInName(network.Name)),
            unusedImages.Sum(image => ByteSize.Parse(image.Size)));
    }

    public void Forget()
    {
        Current = null;
        Storage = null;
    }

    internal static DiskUsage? ParseDiskUsage(string output)
    {
        string[]? fields = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return fields is { Length: >= 4 }
            && long.TryParse(fields[1], out long total)
            && long.TryParse(fields[2], out long used)
                ? new DiskUsage(total, used)
                : null;
    }
}
