using WslcDesktop.Services;

namespace WslcDesktop.Models;

/// <summary>
///     A published release of the app, with the download for this machine when the release has one.
/// </summary>
/// <param name="Version">The version the release's tag names.</param>
/// <param name="Page">The release's page, with its notes.</param>
/// <param name="Asset">The launcher for this architecture, or null when the release has none.</param>
/// <param name="AssetSize">The size of <paramref name="Asset" /> in bytes.</param>
/// <param name="Checksums">The release's SHA256SUMS.txt, or null when it has none.</param>
public sealed record AppRelease(AppVersion Version, Uri Page, Uri? Asset, long AssetSize, Uri? Checksums);
