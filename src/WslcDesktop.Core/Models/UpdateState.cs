using WslcDesktop.Services;

namespace WslcDesktop.Models;

public enum UpdateStatus
{
    Unavailable,
    NotChecked,
    Checking,
    UpToDate,
    Available,
    Downloading,
    RestartRequired,
    Failed
}

/// <summary>
///     Where the app stands with its own updates.
/// </summary>
/// <param name="Status">What the updater is doing or found.</param>
/// <param name="Version">The version on offer or installed; kept by a failure when the update can be tried again.</param>
/// <param name="Progress">The downloaded fraction, from 0 to 1.</param>
/// <param name="Message">Why the last check or update failed.</param>
/// <param name="Page">The release page of <paramref name="Version" />.</param>
public sealed record UpdateState(UpdateStatus Status, AppVersion? Version = null, double Progress = 0, string? Message = null, Uri? Page = null);
