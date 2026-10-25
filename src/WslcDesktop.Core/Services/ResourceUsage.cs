using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Finds which of the given containers use an image, a volume or a network.
/// </summary>
public static class ResourceUsage
{
    private const char Ellipsis = '…';

    public static ContainerDto[] ContainersUsingImage(string imageId, string reference, IEnumerable<ContainerDto> containers) =>
        [.. Ordered(containers).Where(container => IsImage(container.Image, container.ImageId, imageId, reference))];

    public static ContainerDto[] ContainersUsingVolume(string volumeName, IEnumerable<ContainerDto> containers) =>
        [.. Ordered(containers).Where(container => Names(container.Mounts).Any(name => VolumeNameMatches(name, volumeName)))];

    public static ContainerDto[] ContainersUsingNetwork(string networkName, IEnumerable<ContainerDto> containers) =>
        [.. Ordered(containers).Where(container => Names(container.Networks).Contains(networkName))];

    /// <summary>
    ///     Whether a container's image, as the container reports it, is the given image.
    /// </summary>
    public static bool IsImage(string containerImage, string containerImageId, string imageId, string reference)
    {
        if (containerImageId.Length > 0)
        {
            return containerImageId == imageId;
        }

        string shortId = imageId.Replace("sha256:", "", StringComparison.Ordinal);
        return WithDefaultTag(containerImage) == WithDefaultTag(reference)
            || (containerImage.Length >= 12 && shortId.StartsWith(containerImage, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Whether a volume name as a container lists it, possibly cut short with an ellipsis, names the given volume.
    /// </summary>
    public static bool VolumeNameMatches(string listed, string name) =>
        listed.EndsWith(Ellipsis) ? name.StartsWith(listed.TrimEnd(Ellipsis), StringComparison.Ordinal) : listed == name;

    private static IEnumerable<ContainerDto> Ordered(IEnumerable<ContainerDto> containers) =>
        containers.OrderBy(container => container.Names, StringComparer.OrdinalIgnoreCase);

    private static string[] Names(string list) =>
        list.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    ///     "nginx" and "nginx:latest" name the same image; a colon before the last slash is a registry port, not a tag.
    /// </summary>
    private static string WithDefaultTag(string reference) =>
        reference.LastIndexOf(':') > reference.LastIndexOf('/') || reference.Contains('@') ? reference : reference + ":latest";
}
