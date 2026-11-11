using WslcDesktop.Models;
using WslcDesktop.ViewModels;
using static WslcDesktop.Tests.Snapshots;

namespace WslcDesktop.Tests.ViewModels;

public sealed class DashboardAttentionTests
{
    private static readonly Dictionary<string, long> NothingDismissed = [];

    [Theory]
    [InlineData("Exited (1) 3 days ago", " exited with code 1 · 3 days ago")]
    [InlineData("Exited (2)", " exited with code 2")]
    public void Build_ReportsAContainerThatExitedWithAnError(string status, string expected)
    {
        AttentionItem item = Assert.Single(DashboardAttention.Build(Running(Exited("api", status)), NothingDismissed));

        Assert.Equal(AttentionKind.ContainerFailed, item.Kind);
        Assert.Equal(("api", expected, "Critical"), (item.Subject, item.Text, item.Tone));
        Assert.Equal(("Logs", "Start", true), (item.PrimaryAction, item.SecondaryAction, item.CanDismiss));
    }

    [Theory]
    [InlineData("Exited (0) 1 minute ago")]
    [InlineData("Exited (137) 3 days ago")]
    [InlineData("Exited (143) 3 days ago")]
    [InlineData("Created")]
    public void Build_IgnoresAContainerThatStoppedNormally(string status) =>
        Assert.Empty(DashboardAttention.Build(Running(Exited("api", status)), NothingDismissed));

    [Fact]
    public void Build_ReportsARestartLoopThatCannotBeDismissed()
    {
        AttentionItem item = Assert.Single(DashboardAttention.Build(Running(Container("queue", "restarting")), NothingDismissed));

        Assert.Equal(AttentionKind.ContainerRestarting, item.Kind);
        Assert.Equal(("Logs", "Stop", false, "Caution"), (item.PrimaryAction, item.SecondaryAction, item.CanDismiss, item.Tone));
    }

    [Fact]
    public void Build_OrdersFailuresBeforeRestartsBeforeSpace()
    {
        RuntimeSnapshot snapshot = Running(
            [Container("queue", "restarting"), Exited("worker", "Exited (1) now"), Exited("api", "Exited (2) now")],
            new Dictionary<string, ContainerStatsDto>(),
            UnusedImages(2, 2_254_857_830));

        AttentionItem[] items = DashboardAttention.Build(snapshot, NothingDismissed);

        Assert.Equal(["api", "worker", "queue", ""], items.Select(item => item.Subject));
        Assert.Equal("2.1 GiB in 2 unused images", items[^1].Text);
        Assert.Equal("Clean up…", items[^1].PrimaryAction);
    }

    [Fact]
    public void Build_HidesWhatWasDismissed()
    {
        ContainerDto api = Exited("api", "Exited (1) now");
        RuntimeSnapshot snapshot = Running([api], new Dictionary<string, ContainerStatsDto>(), UnusedImages(1, 1000));
        var dismissed = new Dictionary<string, long> { [api.Id] = 0, [DashboardAttention.UnusedImagesKey] = 1000 };

        Assert.Empty(DashboardAttention.Build(snapshot, dismissed));
    }

    [Fact]
    public void Build_ReportsUnusedImagesAgainOnceTheyGrow()
    {
        RuntimeSnapshot snapshot = Running([], new Dictionary<string, ContainerStatsDto>(), UnusedImages(2, 1001));
        var dismissed = new Dictionary<string, long> { [DashboardAttention.UnusedImagesKey] = 1000 };

        Assert.Single(DashboardAttention.Build(snapshot, dismissed));
    }

    [Fact]
    public void Forget_DropsDismissalsOfContainersThatRunAgainOrAreGone()
    {
        ContainerDto failed = Exited("api", "Exited (1) now");
        ContainerDto recovered = Container("web");
        var dismissed = new Dictionary<string, long>
        {
            [failed.Id] = 0,
            [recovered.Id] = 0,
            ["removed-id"] = 0,
            [DashboardAttention.UnusedImagesKey] = 1000
        };

        bool changed = DashboardAttention.Forget(Running(failed, recovered), dismissed);

        Assert.True(changed);
        Assert.Equal([failed.Id, DashboardAttention.UnusedImagesKey], dismissed.Keys);
        Assert.False(DashboardAttention.Forget(Running(failed, recovered), dismissed));
    }

    [Fact]
    public void Summarize_GivesOneFigurePerKind()
    {
        RuntimeSnapshot snapshot = Running(
            [Exited("a", "Exited (1) now"), Exited("b", "Exited (1) now"), Container("queue", "restarting")],
            new Dictionary<string, ContainerStatsDto>(),
            UnusedImages(2, 2_254_857_830));

        Assert.Equal("2 containers failed · 1 restarting · 2.1 GiB reclaimable",
            DashboardAttention.Summarize(DashboardAttention.Build(snapshot, NothingDismissed)));
    }

    private static ContainerDto Exited(string name, string status)
    {
        ContainerDto container = Container(name, "exited");
        container.Status = status;
        return container;
    }

    private static Inventory UnusedImages(int count, long size) => new(count, size, count, 0, 0, 0, 0, size);
}
