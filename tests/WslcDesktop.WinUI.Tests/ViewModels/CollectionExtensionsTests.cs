using System.Collections.ObjectModel;
using System.Collections.Specialized;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Tests.ViewModels;

public sealed class CollectionExtensionsTests
{
    [Fact]
    public void SyncTo_ReordersInsertsAndRemovesInPlace()
    {
        var target = new ObservableCollection<string>(["a", "b", "c", "d"]);

        target.SyncTo(["c", "a", "e"], item => item);

        Assert.Equal(["c", "a", "e"], target);
    }

    [Fact]
    public void SyncTo_KeepsUnchangedItemsWithoutEvents()
    {
        var target = new ObservableCollection<string>(["a", "b"]);
        int changes = 0;
        target.CollectionChanged += (_, _) => changes++;

        target.SyncTo(["a", "b"], item => item);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void SyncTo_ReplacesAChangedItemInsteadOfRemovingAndAddingIt()
    {
        var target = new ObservableCollection<Row>([new Row("a", 1), new Row("b", 1)]);
        List<NotifyCollectionChangedAction> actions = [];
        target.CollectionChanged += (_, e) => actions.Add(e.Action);

        target.SyncTo([new Row("a", 2), new Row("b", 1)], item => item.Key);

        Assert.Equal([new Row("a", 2), new Row("b", 1)], target);
        Assert.Equal([NotifyCollectionChangedAction.Replace], actions);
    }

    [Fact]
    public void SyncTo_MovesAReorderedItemInsteadOfRemovingAndAddingIt()
    {
        var target = new ObservableCollection<Row>([new Row("a", 1), new Row("b", 1), new Row("c", 1)]);
        List<NotifyCollectionChangedAction> actions = [];
        target.CollectionChanged += (_, e) => actions.Add(e.Action);

        target.SyncTo([new Row("c", 2), new Row("a", 1), new Row("b", 1)], item => item.Key);

        Assert.Equal([new Row("c", 2), new Row("a", 1), new Row("b", 1)], target);
        Assert.Equal([NotifyCollectionChangedAction.Move, NotifyCollectionChangedAction.Replace], actions);
    }

    [Fact]
    public void ReplaceWith_SkipsEqualContentAndReplacesOtherwise()
    {
        var target = new ObservableCollection<string>(["a", "b"]);
        var actions = new List<NotifyCollectionChangedAction>();
        target.CollectionChanged += (_, e) => actions.Add(e.Action);

        target.ReplaceWith(["a", "b"]);
        Assert.Empty(actions);

        target.ReplaceWith(["x"]);
        Assert.Equal(["x"], target);
        Assert.Equal([NotifyCollectionChangedAction.Reset, NotifyCollectionChangedAction.Add], actions);
    }

    private sealed record Row(string Key, int Value);
}
