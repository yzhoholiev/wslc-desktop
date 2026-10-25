using WslcDesktop.Models;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.ViewModels;

public sealed class VolumesViewModelTests
{
    private readonly TestServices _test = new();

    [Fact]
    public async Task RemoveAsync_ReloadsTheListEvenWhenTheRemoveFails()
    {
        _test.Services.Monitor.Publish(Running());
        _test.Reader.Volumes = [new VolumeDto { Name = "data" }];
        var viewModel = new VolumesViewModel(_test.Services);
        await viewModel.LoadAsync();
        List<Notification> notifications = [];
        _test.Notifier.Notified += notifications.Add;

        _test.Commands.Failing.Add("volume-remove");
        _test.Reader.Volumes = [];
        await viewModel.RemoveAsync(Assert.Single(viewModel.Items));

        Assert.Equal(["volume-remove data"], _test.Commands.Calls);
        Assert.Equal("Deleting volume data failed", Assert.Single(notifications).Title);
        Assert.Empty(viewModel.Items);
        Assert.False(viewModel.IsBusy);
    }
}
