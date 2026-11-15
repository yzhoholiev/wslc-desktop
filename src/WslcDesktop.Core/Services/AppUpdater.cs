using WslcDesktop.Models;

namespace WslcDesktop.Services;

/// <summary>
///     Finds newer releases of the app and installs one when the user asks.
/// </summary>
public sealed class AppUpdater
{
    /// <summary>
    ///     Several times the size of a release, so only a file that cannot be the launcher is refused.
    /// </summary>
    private const long MaxDownloadBytes = 200L * 1024 * 1024;

    private readonly IInstalledApp _app;
    private readonly IReleaseFeed _feed;
    private readonly Notifier _notifier;
    private readonly ISettingsStore _settings;
    private readonly TimeProvider _time;
    private CancellationTokenSource? _installCancellation;
    private int _isBusy;
    private AppRelease? _release;

    public AppUpdater(IReleaseFeed feed, IInstalledApp app, ISettingsStore settings, Notifier notifier, TimeProvider time)
    {
        _feed = feed;
        _app = app;
        _settings = settings;
        _notifier = notifier;
        _time = time;
        State = InitialState();
    }

    public static Uri ReleasesPage { get; } = new("https://github.com/yzhoholiev/wslc-desktop/releases");

    public UpdateState State { get; private set; }

    /// <summary>
    ///     Whether a version is on offer, including one whose last installation failed.
    /// </summary>
    public bool CanInstall => State is { Status: UpdateStatus.Available or UpdateStatus.Failed, Version: not null };

    public event Action? Changed;

    /// <summary>
    ///     Raised when the launcher holds a newer version and the app should be started again through it.
    /// </summary>
    public event Action? RestartRequested;

    /// <summary>
    ///     Runs the automatic check, which an app does once, as it starts.
    /// </summary>
    public void Start()
    {
        if (_settings.Current.CheckForUpdates)
        {
            _ = CheckAsync(false);
        }
    }

    /// <summary>
    ///     Looks for a newer release. A failure is shown only when the user asked for the check.
    /// </summary>
    public async Task CheckAsync(bool isManual, CancellationToken ct = default)
    {
        if (!_app.HasLauncher || !TryBegin())
        {
            return;
        }

        UpdateState previous = State;
        try
        {
            if (PendingRestart() is { } restart)
            {
                Set(restart);
                return;
            }

            // An automatic check leaves a version on offer in place, so its Update buttons stay usable meanwhile.
            if (isManual || !CanInstall)
            {
                Set(new UpdateState(UpdateStatus.Checking));
            }

            AppRelease? release = await FindNewerAsync(ct);
            _release = release is not null && IsInstallable(release) ? release : null;

            _settings.Current.LastUpdateCheck = _time.GetUtcNow();
            _settings.Current.AvailableUpdate = _release?.Version.ToString();
            _settings.Save();

            Set(release is null ? new UpdateState(UpdateStatus.UpToDate)
                : _release is null ? NotInstallable(release)
                : Offer(release.Version, release.Page));
        }
        catch (OperationCanceledException)
        {
            Set(previous);
        }
        catch (Exception ex)
        {
            Report(ex);
            Set(isManual ? new UpdateState(UpdateStatus.Failed, OfferedVersion(previous), Message: ex.Message, Page: previous.Page) : previous);
        }
        finally
        {
            End();
        }
    }

    /// <summary>
    ///     Downloads the version on offer, checks it against the release's checksums and puts it in the launcher's place.
    /// </summary>
    public async Task InstallAsync()
    {
        if (!CanInstall || !TryBegin())
        {
            return;
        }

        AppVersion version = State.Version!;
        Uri page = State.Page ?? ReleasesPage;
        string? download = null;
        bool isRestartPending = false;
        using var cancellation = new CancellationTokenSource();
        _installCancellation = cancellation;
        try
        {
            // Finding the release and its checksums can take as long as the download, and can be cancelled the same way.
            Set(Downloading(version, 0, page));

            AppRelease? release = _release ?? await FindNewerAsync(cancellation.Token);
            if (release is null)
            {
                Forget();
                Set(new UpdateState(UpdateStatus.UpToDate));
                return;
            }

            if (!IsInstallable(release))
            {
                Forget();
                Set(NotInstallable(release));
                return;
            }

            _release = release;
            version = release.Version;
            page = release.Page;

            // The other side may have updated the launcher since this version was offered; replacing it again could even downgrade it.
            if (_app.ReadLauncherVersion() is { } installed && installed > _app.RunningVersion && installed >= version)
            {
                Forget();
                Set(new UpdateState(UpdateStatus.RestartRequired, installed));
                isRestartPending = true;
                return;
            }

            string expected = ChecksumFile.Find(await _feed.ReadChecksumsAsync(release, cancellation.Token), _app.AssetName)
                ?? throw new UpdateException("The release has no checksum for this download.");

            download = _app.ReserveDownloadPath();
            var progress = new DownloadProgress(fraction => Set(Downloading(version, fraction, page)));
            await _feed.DownloadAsync(release, download, progress, cancellation.Token);

            (long size, string sha256) = await _app.InspectAsync(download, cancellation.Token);
            if (size != release.AssetSize || !string.Equals(sha256, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new UpdateException("The download did not match its checksum.");
            }

            cancellation.Token.ThrowIfCancellationRequested();

            // The swap waits for a starting launcher and retries while a scanner holds the file, which would freeze the caller's thread.
            string verified = download;
            await Task.Run(() => _app.Install(verified));
            download = null;
            Forget();
            Set(new UpdateState(UpdateStatus.RestartRequired, version));

            // A swap cannot be undone, so a cancel that arrives during it only holds back the restart.
            isRestartPending = !cancellation.IsCancellationRequested;
        }
        catch (OperationCanceledException)
        {
            Discard(download);
            Set(Offer(version, page));
        }
        catch (Exception ex)
        {
            Report(ex);
            Discard(download);
            Set(new UpdateState(UpdateStatus.Failed, version, Message: ex.Message, Page: page));
        }
        finally
        {
            _installCancellation = null;
            End();
            if (isRestartPending)
            {
                RestartRequested?.Invoke();
            }
        }
    }

    /// <summary>
    ///     Does what an Update button means: installs the version on offer, or asks for the restart an installed one waits for,
    ///     and notifies of a failure.
    /// </summary>
    public async Task UpdateAsync()
    {
        if (State.Status == UpdateStatus.RestartRequired)
        {
            RequestRestart();
            return;
        }

        if (!CanInstall || Volatile.Read(ref _isBusy) != 0)
        {
            return;
        }

        await InstallAsync();
        if (State is { Status: UpdateStatus.Failed, Message: { } reason })
        {
            _notifier.Error("Could not update WSLC Desktop", reason);
        }
    }

    public void CancelInstall() => _installCancellation?.Cancel();

    /// <summary>
    ///     Asks for the restart that an installed update is waiting for.
    /// </summary>
    public void RequestRestart()
    {
        if (State.Status == UpdateStatus.RestartRequired)
        {
            RestartRequested?.Invoke();
        }
    }

    /// <summary>
    ///     The version on offer, the first time it is asked for; later calls, restarts and the other side get null.
    /// </summary>
    public AppVersion? TakeAnnouncement()
    {
        if (State is not { Status: UpdateStatus.Available, Version: { } version } || _settings.Current.AnnouncedUpdate == version.ToString())
        {
            return null;
        }

        // The other side keeps its own copy of the settings, so what it announced is only in the file.
        bool isAnnouncedByOtherSide = _settings.ReadSaved()?.AnnouncedUpdate == version.ToString();
        _settings.Current.AnnouncedUpdate = version.ToString();
        if (isAnnouncedByOtherSide)
        {
            return null;
        }

        _settings.Save();
        return version;
    }

    private UpdateState InitialState()
    {
        if (!_app.HasLauncher)
        {
            return new UpdateState(UpdateStatus.Unavailable);
        }

        if (PendingRestart() is { } restart)
        {
            return restart;
        }

        if (AppVersion.TryParse(_settings.Current.AvailableUpdate, out AppVersion? remembered) && remembered > _app.RunningVersion)
        {
            return Offer(remembered, new Uri($"{ReleasesPage}/tag/v{remembered}"));
        }

        return new UpdateState(_settings.Current.LastUpdateCheck is null ? UpdateStatus.NotChecked : UpdateStatus.UpToDate);
    }

    /// <summary>
    ///     Set when the launcher file is already newer than this process: the other side updated it, or the user replaced it.
    /// </summary>
    private UpdateState? PendingRestart() =>
        _app.ReadLauncherVersion() is { } installed && installed > _app.RunningVersion
            ? new UpdateState(UpdateStatus.RestartRequired, installed)
            : null;

    private static UpdateState Offer(AppVersion version, Uri page) => new(UpdateStatus.Available, version, Page: page);

    private static UpdateState Downloading(AppVersion version, double fraction, Uri page) =>
        new(UpdateStatus.Downloading, version, Math.Floor(Math.Clamp(fraction, 0, 1) * 100) / 100, Page: page);

    /// <summary>
    ///     A newer release without a version to install, so that no surface offers an update that can only fail.
    /// </summary>
    private static UpdateState NotInstallable(AppRelease release) => new(
        UpdateStatus.Failed,
        Message: $"Version {release.Version} is available, but cannot be installed from the app.",
        Page: release.Page);

    private static bool IsInstallable(AppRelease release) =>
        release is { Asset: not null, Checksums: not null, AssetSize: <= MaxDownloadBytes };

    private static AppVersion? OfferedVersion(UpdateState state) =>
        state.Status is UpdateStatus.Available or UpdateStatus.Failed ? state.Version : null;

    /// <summary>
    ///     Keeps a failure the ports did not foresee, which would otherwise show only as its message.
    /// </summary>
    private static void Report(Exception exception)
    {
        if (exception is not UpdateException)
        {
            CrashLog.Write(exception);
        }
    }

    private async Task<AppRelease?> FindNewerAsync(CancellationToken ct)
    {
        AppRelease? release = await _feed.FindLatestAsync(_app.RunningVersion.IsPreRelease, ct);
        return release is not null && release.Version > _app.RunningVersion ? release : null;
    }

    private void Forget()
    {
        _release = null;
        _settings.Current.AvailableUpdate = null;
        _settings.Save();
    }

    private void Discard(string? download)
    {
        if (download is not null)
        {
            _app.Discard(download);
        }
    }

    private bool TryBegin() => Interlocked.CompareExchange(ref _isBusy, 1, 0) == 0;

    private void End() => Interlocked.Exchange(ref _isBusy, 0);

    private void Set(UpdateState state)
    {
        if (state == State)
        {
            return;
        }

        State = state;
        Changed?.Invoke();
    }

    /// <summary>
    ///     Reports on the caller's thread, unlike <see cref="Progress{T}" />, which posts and so can arrive after the download ended.
    /// </summary>
    private sealed class DownloadProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
