using System.Text.RegularExpressions;
using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     The equivalent of <c>docker system prune</c>, which wslc does not provide, built from the per-resource prune commands.
/// </summary>
public sealed partial class SystemCleanup(IRuntimeCommands commands, RuntimeActions actions)
{
    public async Task<CleanupResult> RunAsync(CleanupOptions options)
    {
        long reclaimed = 0;
        var failures = new List<string>();

        if (options.StopRunningContainers)
        {
            try
            {
                await actions.StopAllContainersAsync();
            }
            catch (CliException ex)
            {
                // Pruning still frees whatever is already unused; the containers that did not stop are kept.
                failures.Add($"stopping containers: {ex.Message}");
            }
        }

        // Containers go first so that the images, networks and volumes they held become unused.
        List<(bool Enabled, string Name, Func<Task<string>> Prune)> steps =
        [
            (options.StoppedContainers, "containers", commands.PruneContainersAsync),
            (options.UnusedNetworks, "networks", commands.PruneNetworksAsync),
            (options.UnusedImages, "images", () => commands.PruneImagesAsync(options.IncludeTaggedImages)),
            (options.UnusedVolumes, "volumes", () => commands.PruneVolumesAsync(options.IncludeNamedVolumes))
        ];

        foreach ((bool _, string? name, Func<Task<string>>? prune) in steps.Where(step => step.Enabled))
        {
            try
            {
                reclaimed += ParseReclaimed(await prune());
            }
            catch (CliException ex)
            {
                failures.Add($"{name}: {ex.Message}");
            }
        }

        return new CleanupResult(reclaimed, failures);
    }

    internal static long ParseReclaimed(string output) =>
        ReclaimedPattern().Match(output) is { Success: true } match ? ByteSize.Parse(match.Groups[1].Value) : 0;

    [GeneratedRegex(@"Total reclaimed space:\s*(\S+)")]
    private static partial Regex ReclaimedPattern();
}
