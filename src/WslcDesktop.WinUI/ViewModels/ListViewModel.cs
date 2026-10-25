using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

/// <summary>
///     Searchable, sortable list page with a details pane for the selected item.
/// </summary>
public abstract partial class ListViewModel<TItem> : PageViewModel
    where TItem : class
{
    private readonly IReadOnlyDictionary<string, SortColumn<TItem>> _sortColumns;

    protected ListViewModel(AppServices services, string list, string defaultSortKey, IReadOnlyDictionary<string, SortColumn<TItem>> sortColumns)
        : base(services)
    {
        _sortColumns = sortColumns;
        Sort = ListSort.For(services.Settings, list, defaultSortKey, sortColumns);
        Sort.Changed += (_, _) => ApplyFilter();
    }

    public ObservableCollection<TItem> Items { get; } = [];

    public ListSort Sort { get; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial string Summary { get; set; } = "";

    [ObservableProperty]
    public partial bool IsEmpty { get; set; } = true;

    [ObservableProperty]
    public partial EmptyText Empty { get; set; } = EmptyText.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailsOpen))]
    public partial TItem? Selected { get; set; }

    [ObservableProperty]
    public partial string InspectText { get; set; } = "";

    public bool IsDetailsOpen => Selected is not null;

    [RelayCommand]
    public void CloseDetails() => Selected = null;

    public virtual Task RunEmptyActionAsync()
    {
        switch (Empty.Reason)
        {
            case EmptyReason.RuntimeStopped:
                return StartRuntimeAsync();
            case EmptyReason.NoMatches:
                SearchText = "";
                break;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Name that orders rows whose sort values are equal.
    /// </summary>
    protected abstract string NameOf(TItem item);

    /// <summary>
    ///     Identity that survives a reload, which recreates the item records.
    /// </summary>
    protected abstract string KeyOf(TItem item);

    /// <summary>
    ///     The items that pass the page's filters and the search text.
    /// </summary>
    protected abstract IEnumerable<TItem> Filter(string search);

    /// <summary>
    ///     What the list shows instead of rows when no item passes the filters.
    /// </summary>
    protected abstract EmptyText DescribeEmpty(string search);

    /// <summary>
    ///     Loads the details pane for another item, or none, as opposed to a newer record of the same item.
    /// </summary>
    protected abstract void OnSelectionChanged(TItem? item);

    protected virtual void ApplyFilter()
    {
        string search = SearchText.Trim();
        Items.SyncTo([.. Sort.Apply(Filter(search), _sortColumns, NameOf)], KeyOf);
        IsEmpty = Items.Count == 0;
        Empty = IsEmpty ? DescribeEmpty(search) : EmptyText.None;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedChanged(TItem? oldValue, TItem? newValue)
    {
        if (oldValue is not null && newValue is not null && KeyOf(oldValue) == KeyOf(newValue))
        {
            OnSelectionRefreshed(newValue);
        }
        else
        {
            OnSelectionChanged(newValue);
        }
    }

    /// <summary>
    ///     Called when the selection moves to a newer record of the same item, which keeps its details pane.
    /// </summary>
    protected virtual void OnSelectionRefreshed(TItem item)
    {
    }
}
