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

    [Fact]
    public async Task LoadAsync_FillsInSizes()
    {
        Sizes(("data", 1536));

        VolumesViewModel viewModel = await LoadedAsync("data");

        VolumeItem item = Assert.Single(viewModel.Items);
        Assert.Equal(1536, item.SizeBytes);
        Assert.Equal(ByteSize.Format(1536), item.Size);
    }

    public static TheoryData<long, string> ExactSizes => new()
    {
        { 316416215, "316,416,215 bytes" },
        { 1, "1 byte" },
        { 0, "0 bytes" }
    };

    [Theory]
    [MemberData(nameof(ExactSizes))]
    public void SizeInBytes_SpellsOutTheExactSize(long bytes, string expected) =>
        Assert.Equal(expected, new VolumeItem("data", "data", "local", "/data", false, 0, bytes).SizeInBytes);

    [Fact]
    public void SizeInBytes_IsEmptyWhileTheSizeIsUnknown() =>
        Assert.Null(new VolumeItem("data", "data", "local", "/data", false, 0, null).SizeInBytes);

    [Fact]
    public async Task LoadAsync_ShowsTheListBeforeSizesArrive()
    {
        TaskCompletionSource<IReadOnlyDictionary<string, long>> pending = PendingSizes();

        VolumesViewModel viewModel = await LoadedAsync("data");

        Assert.Equal("—", Assert.Single(viewModel.Items).Size);

        pending.SetResult(new Dictionary<string, long> { ["data"] = 1536 });

        Assert.Equal(1536, Assert.Single(viewModel.Items).SizeBytes);
    }

    [Fact]
    public async Task LoadAsync_KeepsTheListWhenSizesFail()
    {
        _test.Details.ReadVolumeSizes = _ => Task.FromException<IReadOnlyDictionary<string, long>>(new CliException("no engine"));

        VolumesViewModel viewModel = await LoadedAsync("data");

        Assert.Equal("—", Assert.Single(viewModel.Items).Size);
        Assert.False(viewModel.HasLoadError);
    }

    [Fact]
    public async Task LoadAsync_ShowsZeroBytesAsASize()
    {
        Sizes(("data", 0));

        VolumesViewModel viewModel = await LoadedAsync("data");

        Assert.Equal(ByteSize.Format(0), Assert.Single(viewModel.Items).Size);
    }

    [Fact]
    public async Task LoadAsync_IgnoresSizesOfVolumesNotListed()
    {
        Sizes(("data", 1), ("gone", 2));

        VolumesViewModel viewModel = await LoadedAsync("data");

        Assert.Equal(1, Assert.Single(viewModel.Items).SizeBytes);
    }

    [Fact]
    public async Task LoadAsync_KeepsKnownSizesUntilNewOnesArrive()
    {
        Sizes(("data", 10));
        VolumesViewModel viewModel = await LoadedAsync("data");
        TaskCompletionSource<IReadOnlyDictionary<string, long>> pending = PendingSizes();

        await viewModel.LoadAsync();

        Assert.Equal(10, Assert.Single(viewModel.Items).SizeBytes);

        pending.SetResult(new Dictionary<string, long> { ["data"] = 20 });

        Assert.Equal(20, Assert.Single(viewModel.Items).SizeBytes);
    }

    [Fact]
    public async Task LoadAsync_IgnoresAnOlderSizeAnswer()
    {
        TaskCompletionSource<IReadOnlyDictionary<string, long>> older = PendingSizes();
        VolumesViewModel viewModel = await LoadedAsync("data");
        TaskCompletionSource<IReadOnlyDictionary<string, long>> newer = PendingSizes();
        await viewModel.LoadAsync();

        newer.SetResult(new Dictionary<string, long> { ["data"] = 20 });
        older.SetResult(new Dictionary<string, long> { ["data"] = 10 });

        Assert.Equal(20, Assert.Single(viewModel.Items).SizeBytes);
    }

    [Fact]
    public async Task Sort_OrdersBySizeInBytes()
    {
        // As text "900 B" sorts above "2.0 KiB".
        Sizes(("a", 900), ("b", 2048));
        VolumesViewModel viewModel = await LoadedAsync("a", "b", "c");

        viewModel.Sort.ChangeCommand.Execute("size");

        Assert.Equal(["b", "a", "c"], viewModel.Items.Select(item => item.Name));

        viewModel.Sort.ChangeCommand.Execute("size");

        Assert.Equal(["c", "a", "b"], viewModel.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task Summary_EndsWithTheTotalOfKnownSizes()
    {
        Sizes(("a", 1024), ("b", 1024));

        VolumesViewModel viewModel = await LoadedAsync("a", "b", "c");

        Assert.EndsWith($" · {ByteSize.Format(2048)}", viewModel.Summary);
    }

    [Fact]
    public async Task Summary_HasNoTotalWhileNoSizeIsKnown()
    {
        VolumesViewModel viewModel = await LoadedAsync("data");

        Assert.Equal("1 volume · 0 anonymous · 1 unused", viewModel.Summary);
    }

    [Fact]
    public async Task Selected_KeepsTheSelectionAndShowsTheSizeWhenSizesArrive()
    {
        TaskCompletionSource<IReadOnlyDictionary<string, long>> pending = PendingSizes();
        VolumesViewModel viewModel = await LoadedAsync("data");
        viewModel.Selected = Assert.Single(viewModel.Items);

        pending.SetResult(new Dictionary<string, long> { ["data"] = 1536 });

        Assert.Equal(1536, viewModel.Selected?.SizeBytes);
        Assert.Contains(ByteSize.Format(1536), viewModel.DetailsSubtitle);
    }

    [Fact]
    public async Task LoadAsync_CancelsTheSizeReadItSupersedes()
    {
        List<CancellationToken> reads = [];
        _test.Details.ReadVolumeSizes = ct =>
        {
            reads.Add(ct);
            return new TaskCompletionSource<IReadOnlyDictionary<string, long>>().Task;
        };
        VolumesViewModel viewModel = await LoadedAsync("data");

        await viewModel.LoadAsync();

        Assert.Equal(2, reads.Count);
        Assert.True(reads[0].IsCancellationRequested);
        Assert.False(reads[1].IsCancellationRequested);
    }

    [Fact]
    public async Task LoadAsync_KeepsTheListWhenTheSizeReadIsCancelled()
    {
        _test.Details.ReadVolumeSizes = _ => Task.FromCanceled<IReadOnlyDictionary<string, long>>(new CancellationToken(true));

        VolumesViewModel viewModel = await LoadedAsync("data");

        Assert.Equal("—", Assert.Single(viewModel.Items).Size);
        Assert.False(viewModel.HasLoadError);
    }

    [Fact]
    public async Task LoadAsync_IgnoresSizesThatArriveAfterTheRuntimeStopped()
    {
        TaskCompletionSource<IReadOnlyDictionary<string, long>> pending = PendingSizes();
        VolumesViewModel viewModel = await LoadedAsync("data");
        _test.Services.Monitor.Publish(Stopped());
        await viewModel.LoadAsync();
        string stopped = viewModel.Summary;

        pending.SetResult(new Dictionary<string, long> { ["data"] = 1536 });

        Assert.Equal(stopped, viewModel.Summary);
        Assert.Empty(viewModel.Items);
    }

    [Fact]
    public async Task LoadAsync_CancelsTheSizeReadWhenTheRuntimeStops()
    {
        CancellationToken read = default;
        _test.Details.ReadVolumeSizes = ct =>
        {
            read = ct;
            return new TaskCompletionSource<IReadOnlyDictionary<string, long>>().Task;
        };
        VolumesViewModel viewModel = await LoadedAsync("data");

        _test.Services.Monitor.Publish(Stopped());
        await viewModel.LoadAsync();

        Assert.True(read.IsCancellationRequested);
    }

    [Fact]
    public async Task LoadAsync_DropsSizesItCouldNotReadAgain()
    {
        Sizes(("data", 10));
        VolumesViewModel viewModel = await LoadedAsync("data");
        _test.Details.ReadVolumeSizes = _ => Task.FromException<IReadOnlyDictionary<string, long>>(new CliException("no engine"));

        await viewModel.LoadAsync();

        Assert.Equal("—", Assert.Single(viewModel.Items).Size);
        Assert.Equal("1 volume · 0 anonymous · 1 unused", viewModel.Summary);
    }

    private async Task<VolumesViewModel> LoadedAsync(params string[] volumes)
    {
        _test.Services.Monitor.Publish(Running());
        _test.Reader.Volumes = [.. volumes.Select(name => new VolumeDto { Name = name })];
        var viewModel = new VolumesViewModel(_test.Services);
        await viewModel.LoadAsync();
        return viewModel;
    }

    private void Sizes(params (string Name, long Bytes)[] sizes) =>
        _test.Details.ReadVolumeSizes = _ =>
            Task.FromResult<IReadOnlyDictionary<string, long>>(sizes.ToDictionary(size => size.Name, size => size.Bytes));

    /// <summary>
    ///     Makes the next size read wait for the returned source.
    /// </summary>
    private TaskCompletionSource<IReadOnlyDictionary<string, long>> PendingSizes()
    {
        var pending = new TaskCompletionSource<IReadOnlyDictionary<string, long>>();
        _test.Details.ReadVolumeSizes = _ => pending.Task;
        return pending;
    }
}
