using WslcDesktop.Models;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.ViewModels;

public sealed class DashboardUpdateAttentionTests
{
    private const string Hash = "ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12";

    private static readonly AppVersion Next = FakeInstalledApp.Version("1.2.0");
    private static readonly Dictionary<string, long> NothingDismissed = [];

    private readonly TestServices _test = new();

    [Fact]
    public void ForUpdate_OffersTheAvailableVersion()
    {
        AttentionItem? item = DashboardAttention.ForUpdate(new UpdateState(UpdateStatus.Available, Next), NothingDismissed);

        Assert.NotNull(item);
        Assert.Equal((AttentionKind.AppUpdate, "update:1.2.0", "WSLC Desktop 1.2.0 is available"), (item.Kind, item.Key, item.Text));
        Assert.Equal(("Update", "", "Caution", true), (item.PrimaryAction, item.SecondaryAction, item.Tone, item.CanDismiss));
    }

    [Fact]
    public void ForUpdate_OffersTheVersionAgain_AfterAFailedInstallation()
    {
        AttentionItem? item = DashboardAttention.ForUpdate(new UpdateState(UpdateStatus.Failed, Next, Message: "No."), NothingDismissed);

        Assert.Equal(AttentionKind.AppUpdate, item?.Kind);
    }

    [Fact]
    public void ForUpdate_ShowsTheDownload_WithoutActions()
    {
        AttentionItem? item = DashboardAttention.ForUpdate(new UpdateState(UpdateStatus.Downloading, Next, 0.4), NothingDismissed);

        Assert.NotNull(item);
        Assert.Equal((AttentionKind.AppUpdating, "Downloading WSLC Desktop 1.2.0… 40%"), (item.Kind, item.Text));
        Assert.Equal(("", "", false), (item.PrimaryAction, item.SecondaryAction, item.CanDismiss));
    }

    [Fact]
    public void ForUpdate_OffersTheRestart()
    {
        AttentionItem? item = DashboardAttention.ForUpdate(new UpdateState(UpdateStatus.RestartRequired, Next), NothingDismissed);

        Assert.NotNull(item);
        Assert.Equal((AttentionKind.AppRestart, "update:1.2.0", "Restart to finish updating to 1.2.0"), (item.Kind, item.Key, item.Text));
        Assert.Equal("Restart", item.PrimaryAction);
    }

    [Theory]
    [InlineData(UpdateStatus.Available)]
    [InlineData(UpdateStatus.RestartRequired)]
    public void ForUpdate_ReturnsNull_ForADismissedVersion(UpdateStatus status) =>
        Assert.Null(DashboardAttention.ForUpdate(new UpdateState(status, Next), new Dictionary<string, long> { ["update:1.2.0"] = 1 }));

    [Fact]
    public void ForUpdate_ReturnsAgain_ForANewerVersion()
    {
        var state = new UpdateState(UpdateStatus.Available, FakeInstalledApp.Version("1.3.0"));

        Assert.NotNull(DashboardAttention.ForUpdate(state, new Dictionary<string, long> { ["update:1.2.0"] = 1 }));
    }

    [Theory]
    [InlineData(UpdateStatus.Unavailable)]
    [InlineData(UpdateStatus.NotChecked)]
    [InlineData(UpdateStatus.Checking)]
    [InlineData(UpdateStatus.UpToDate)]
    [InlineData(UpdateStatus.Failed)]
    public void ForUpdate_ReturnsNull_WhenThereIsNothingToInstall(UpdateStatus status) =>
        Assert.Null(DashboardAttention.ForUpdate(new UpdateState(status), NothingDismissed));

    [Fact]
    public void Forget_KeepsTheDismissedUpdate()
    {
        var dismissed = new Dictionary<string, long> { ["update:1.2.0"] = 1, ["gone-container"] = 0 };

        Assert.True(DashboardAttention.Forget(Running(), dismissed));

        Assert.Equal(["update:1.2.0"], dismissed.Keys);
    }

    [Fact]
    public void Summarize_NamesTheUpdate()
    {
        AttentionItem update = DashboardAttention.ForUpdate(new UpdateState(UpdateStatus.Available, Next), NothingDismissed)!;
        AttentionItem restart = DashboardAttention.ForUpdate(new UpdateState(UpdateStatus.RestartRequired, Next), NothingDismissed)!;

        Assert.Equal("app update available", DashboardAttention.Summarize([update]));
        Assert.Equal("restart to finish updating", DashboardAttention.Summarize([restart]));
    }

    [Fact]
    public async Task Attention_ShowsTheUpdate_WhileTheRuntimeIsStopped()
    {
        DashboardViewModel viewModel = await CreateWithOfferAsync();

        viewModel.Apply(Stopped());

        Assert.True(viewModel.HasAttention);
        Assert.Equal(AttentionKind.AppUpdate, viewModel.SingleAttention?.Kind);
        Assert.Equal("Caution", viewModel.AttentionTone);
    }

    [Fact]
    public async Task Attention_ListsTheUpdateFirst()
    {
        DashboardViewModel viewModel = await CreateWithOfferAsync();

        ContainerDto failed = Container("api", "exited");
        failed.Status = "Exited (1) now";

        viewModel.Apply(Running(failed));

        Assert.True(viewModel.HasAttentionList);
        Assert.Equal([AttentionKind.AppUpdate, AttentionKind.ContainerFailed], viewModel.AttentionItems.Select(item => item.Kind));
        Assert.Equal("Critical", viewModel.AttentionTone);
        Assert.Equal("", viewModel.MoreAttentionText);
    }

    [Fact]
    public async Task Dismiss_HidesTheUpdateUntilANewerVersion()
    {
        _test.Settings.Current.DismissedAttention["update:1.1.5"] = 1;
        DashboardViewModel viewModel = await CreateWithOfferAsync();
        viewModel.Apply(Stopped());

        viewModel.Dismiss(viewModel.SingleAttention!);

        Assert.False(viewModel.HasAttention);
        Assert.Equal(["update:1.2.0"], _test.Settings.Current.DismissedAttention.Keys);

        _test.Releases.Latest = Release("1.3.0");
        await _test.Services.Updater.CheckAsync(true, TestContext.Current.CancellationToken);
        viewModel.Apply(Stopped());
        Assert.Equal("WSLC Desktop 1.3.0 is available", viewModel.SingleAttention?.Text);
    }

    [Fact]
    public async Task UpdateAppAsync_InstallsTheUpdate()
    {
        DashboardViewModel viewModel = await CreateWithOfferAsync();
        int restarts = 0;
        _test.Services.Updater.RestartRequested += () => restarts++;

        await viewModel.UpdateAppAsync();

        Assert.Single(_test.Installed.Installed);
        Assert.Equal(1, restarts);
    }

    [Fact]
    public async Task UpdateAppAsync_ReportsAFailure()
    {
        _test.Installed.InstallFailure = new UpdateException("WSLC Desktop could not be replaced.");
        DashboardViewModel viewModel = await CreateWithOfferAsync();
        List<Notification> notifications = [];
        _test.Notifier.Notified += notifications.Add;

        await viewModel.UpdateAppAsync();

        Notification notification = Assert.Single(notifications);
        Assert.Equal(("Could not update WSLC Desktop", "WSLC Desktop could not be replaced.", NotificationSeverity.Error),
            (notification.Title, notification.Message, notification.Severity));
    }

    [Fact]
    public async Task UpdateAppAsync_AsksForTheRestart_WhenTheUpdateIsInstalled()
    {
        DashboardViewModel viewModel = await CreateWithOfferAsync();
        await viewModel.UpdateAppAsync();
        int restarts = 0;
        _test.Services.Updater.RestartRequested += () => restarts++;

        await viewModel.UpdateAppAsync();

        Assert.Equal(1, restarts);
        Assert.Single(_test.Installed.Installed);
    }

    private async Task<DashboardViewModel> CreateWithOfferAsync()
    {
        _test.Releases.Latest = Release("1.2.0");
        _test.Releases.Checksums = $"{Hash}  WslcDesktop-win-x64.exe";
        _test.Installed.DownloadedSize = 1000;
        _test.Installed.DownloadedSha256 = Hash;
        await _test.Services.Updater.CheckAsync(true, TestContext.Current.CancellationToken);
        return new DashboardViewModel(_test.Services);
    }

    private static AppRelease Release(string version) => new(
        FakeInstalledApp.Version(version),
        new Uri($"https://github.com/yzhoholiev/wslc-desktop/releases/tag/v{version}"),
        new Uri($"https://github.com/yzhoholiev/wslc-desktop/releases/download/v{version}/WslcDesktop-win-x64.exe"),
        1000,
        new Uri($"https://github.com/yzhoholiev/wslc-desktop/releases/download/v{version}/SHA256SUMS.txt"));
}
