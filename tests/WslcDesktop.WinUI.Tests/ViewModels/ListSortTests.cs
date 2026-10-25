using WslcDesktop.ViewModels;

namespace WslcDesktop.Tests.ViewModels;

public sealed class ListSortTests
{
    private static readonly Dictionary<string, SortColumn<Row>> Columns = new()
    {
        ["name"] = new SortColumn<Row>(row => row.Name),
        ["size"] = new SortColumn<Row>(row => row.Size, true)
    };

    private static readonly Row[] Rows = [new("beta", 10), new("Alpha", 30), new("gamma", 10)];
    private readonly TestServices _test = new();

    [Fact]
    public void Apply_UsesTheDefaultColumnAscendingAndIgnoresCase()
    {
        var sort = ListSort.For(_test.Settings, NewList(), "name", Columns);

        Assert.Equal(["Alpha", "beta", "gamma"], Names(sort));
    }

    [Fact]
    public void Change_StartsNumericColumnsDescendingAndBreaksTiesByName()
    {
        var sort = ListSort.For(_test.Settings, NewList(), "name", Columns);

        sort.ChangeCommand.Execute("size");

        Assert.Equal("size", sort.Key);
        Assert.True(sort.IsDescending);
        Assert.Equal(["Alpha", "beta", "gamma"], Names(sort));
    }

    [Fact]
    public void Change_OnTheActiveColumnReversesIt()
    {
        var sort = ListSort.For(_test.Settings, NewList(), "name", Columns);

        sort.ChangeCommand.Execute("name");

        Assert.True(sort.IsDescending);
        Assert.Equal(["gamma", "beta", "Alpha"], Names(sort));
    }

    [Fact]
    public void Change_RaisesChangedAndRemembersTheOrder()
    {
        string list = NewList();
        var sort = ListSort.For(_test.Settings, list, "name", Columns);
        int raised = 0;
        sort.Changed += (_, _) => raised++;

        sort.ChangeCommand.Execute("size");
        var restored = ListSort.For(_test.Settings, list, "name", Columns);

        Assert.Equal(1, raised);
        Assert.Equal("size:desc", _test.Settings.Current.SortOrders[list]);
        Assert.Equal("size", restored.Key);
        Assert.True(restored.IsDescending);
    }

    [Fact]
    public void Apply_FallsBackToTheDefaultForAnUnknownSavedColumn()
    {
        string list = NewList();
        _test.Settings.Current.SortOrders[list] = "removed-column:desc";
        var sort = ListSort.For(_test.Settings, list, "name", Columns);

        Assert.Equal(["Alpha", "beta", "gamma"], Names(sort));
        Assert.Equal("name", sort.Key);
        Assert.False(sort.IsDescending);
    }

    private static string NewList() => $"test-{Guid.NewGuid():N}";

    private static IEnumerable<string> Names(ListSort sort) => sort.Apply(Rows, Columns, row => row.Name).Select(row => row.Name);

    private sealed record Row(string Name, int Size);
}
