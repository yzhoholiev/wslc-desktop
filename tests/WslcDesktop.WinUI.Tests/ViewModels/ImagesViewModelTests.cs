using WslcDesktop.Models;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.ViewModels;

public sealed class ImagesViewModelTests
{
    private readonly TestServices _test = new();

    [Theory]
    [InlineData("--help")]
    [InlineData("nginx; calc")]
    public async Task Pull_RefusesAnInvalidReferenceWithoutRunningWslc(string reference)
    {
        List<Notification> notifications = [];
        _test.Notifier.Notified += notifications.Add;
        var viewModel = new ImagesViewModel(_test.Services) { PullReference = reference };

        await viewModel.PullCommand.ExecuteAsync(null);

        Assert.Empty(_test.Commands.Calls);
        Assert.Equal($"Cannot pull '{reference}'", Assert.Single(notifications).Title);
        Assert.Equal(reference, viewModel.PullReference);
    }

    [Fact]
    public async Task Pull_PullsTheTrimmedReferenceAndClearsTheBox()
    {
        _test.Services.Monitor.Publish(Running());
        var viewModel = new ImagesViewModel(_test.Services) { PullReference = " nginx:alpine " };

        await viewModel.PullCommand.ExecuteAsync(null);

        Assert.Equal(["pull nginx:alpine"], _test.Commands.Calls);
        Assert.Equal("", viewModel.PullReference);
    }

    [Fact]
    public async Task Summary_CountsAnImageWithSeveralTagsOnce()
    {
        _test.Services.Monitor.Publish(Running());
        _test.Reader.Images =
        [
            new ImageDto { Id = "sha256:a", Repository = "app", Tag = "1.0", Size = "1MB", Containers = "0" },
            new ImageDto { Id = "sha256:a", Repository = "app", Tag = "latest", Size = "1MB", Containers = "0" },
            new ImageDto { Id = "sha256:b", Repository = "db", Tag = "16", Size = "2MB", Containers = "1" }
        ];
        var viewModel = new ImagesViewModel(_test.Services);

        await viewModel.LoadAsync();

        Assert.Equal(3, viewModel.Items.Count);
        Assert.Equal("2 images · 2.9 MiB · 1 unused", viewModel.Summary);
    }
}
