namespace WslcDesktop.Models;

public sealed record CleanupOptions(
    bool StopRunningContainers,
    bool StoppedContainers,
    bool UnusedNetworks,
    bool UnusedImages,
    bool IncludeTaggedImages,
    bool UnusedVolumes,
    bool IncludeNamedVolumes)
{
    /// <summary>
    ///     Whether any prune step is chosen; stopping containers alone frees nothing.
    /// </summary>
    public bool HasAny => StoppedContainers || UnusedNetworks || UnusedImages || UnusedVolumes;
}

public sealed record CleanupSummary(int RunningContainers, int StoppedContainers, int Images, string ImagesSize, int Volumes);

public sealed record CleanupResult(long ReclaimedBytes, IReadOnlyList<string> Failures);
