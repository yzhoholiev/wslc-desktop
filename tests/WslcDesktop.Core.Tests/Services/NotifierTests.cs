using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class NotifierTests
{
    private readonly Notifier _notifier = new();

    [Fact]
    public async Task RunAsync_ReportsSuccess_WhenOperationCompletesWithAMessage()
    {
        (bool succeeded, List<Notification>? seen) = await CaptureAsync(() => _notifier.RunAsync("Pulling nginx", () => Task.CompletedTask, "Pulled nginx."));

        Assert.True(succeeded);
        Notification notification = Assert.Single(seen);
        Assert.Equal(NotificationSeverity.Success, notification.Severity);
        Assert.Equal("Done", notification.Title);
        Assert.Equal("Pulled nginx.", notification.Message);
    }

    [Fact]
    public async Task RunAsync_ReportsNothing_WhenSuccessHasNoMessage()
    {
        (bool succeeded, List<Notification>? seen) = await CaptureAsync(() => _notifier.RunAsync("Pulling nginx", () => Task.CompletedTask));

        Assert.True(succeeded);
        Assert.Empty(seen);
    }

    [Fact]
    public async Task RunAsync_ReportsCliFailureWithItsMessage()
    {
        (bool succeeded, List<Notification>? seen) = await CaptureAsync(() => _notifier.RunAsync("Pulling nginx", () => throw new CliException("boom")));

        Assert.False(succeeded);
        Notification notification = Assert.Single(seen);
        Assert.Equal(NotificationSeverity.Error, notification.Severity);
        Assert.Equal("Pulling nginx failed", notification.Title);
        Assert.Equal("boom", notification.Message);
    }

    private async Task<(bool Succeeded, List<Notification> Seen)> CaptureAsync(Func<Task<bool>> run)
    {
        List<Notification> seen = [];

        void Record(Notification notification)
        {
            seen.Add(notification);
        }

        _notifier.Notified += Record;
        try
        {
            return (await run(), seen);
        }
        finally
        {
            _notifier.Notified -= Record;
        }
    }
}
