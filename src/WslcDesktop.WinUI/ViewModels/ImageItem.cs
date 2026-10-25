namespace WslcDesktop.ViewModels;

public sealed record ImageItem(
    string Id,
    string Reference,
    string Repository,
    string Tag,
    string ShortId,
    string Size,
    long SizeBytes,
    string Created,
    string CreatedAt,
    int ContainerCount)
{
    public bool IsUnused => ContainerCount == 0;

    public bool HasTag => Tag is not ("" or "<none>");

    /// <summary>
    ///     Size relative to the largest image in the list, from 0 to 1.
    /// </summary>
    public double SizeShare { get; init; }
}
