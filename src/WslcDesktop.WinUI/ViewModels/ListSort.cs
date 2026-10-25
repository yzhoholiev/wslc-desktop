using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslcDesktop.Services;

namespace WslcDesktop.ViewModels;

/// <summary>
///     A sortable column: the value rows are ordered by, and whether a first click sorts it high-to-low.
/// </summary>
public sealed record SortColumn<TItem>(Func<TItem, IComparable> Value, bool DescendingFirst = false);

/// <summary>
///     The column and direction one list is sorted by, remembered across sessions.
/// </summary>
public sealed partial class ListSort : ObservableObject
{
    private const string Ascending = "asc";
    private const string Descending = "desc";
    private readonly string _defaultKey;
    private readonly IReadOnlySet<string> _descendingFirst;

    private readonly string _list;
    private readonly ISettingsStore _settings;

    private ListSort(ISettingsStore settings, string list, string defaultKey, IReadOnlySet<string> descendingFirst)
    {
        _settings = settings;
        _list = list;
        _defaultKey = defaultKey;
        _descendingFirst = descendingFirst;
        Key = defaultKey;

        if (settings.Current.SortOrders.GetValueOrDefault(list)?.Split(':') is not [var key, var direction])
        {
            return;
        }

        Key = key;
        IsDescending = direction == Descending;
    }

    [ObservableProperty]
    public partial string Key { get; set; }

    [ObservableProperty]
    public partial bool IsDescending { get; set; }

    public event EventHandler? Changed;

    public static ListSort For<TItem>(ISettingsStore settings, string list, string defaultKey, IReadOnlyDictionary<string, SortColumn<TItem>> columns) =>
        new(settings, list, defaultKey, columns.Where(column => column.Value.DescendingFirst).Select(column => column.Key).ToHashSet());

    public IEnumerable<TItem> Apply<TItem>(IEnumerable<TItem> items, IReadOnlyDictionary<string, SortColumn<TItem>> columns, Func<TItem, string> nameOf)
    {
        if (!columns.TryGetValue(Key, out SortColumn<TItem>? column))
        {
            Key = _defaultKey;
            IsDescending = false;
            column = columns[_defaultKey];
        }

        IOrderedEnumerable<TItem> ordered = IsDescending
            ? items.OrderByDescending(column.Value, ValueComparer.Instance)
            : items.OrderBy(column.Value, ValueComparer.Instance);

        return ordered.ThenBy(nameOf, StringComparer.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void Change(string key)
    {
        IsDescending = key == Key ? !IsDescending : _descendingFirst.Contains(key);
        Key = key;
        _settings.Current.SortOrders[_list] = $"{key}:{(IsDescending ? Descending : Ascending)}";
        _settings.Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class ValueComparer : IComparer<IComparable>
    {
        public static ValueComparer Instance { get; } = new();

        public int Compare(IComparable? x, IComparable? y) => x is string left && y is string right
            ? StringComparer.OrdinalIgnoreCase.Compare(left, right)
            : Comparer<IComparable>.Default.Compare(x, y);
    }
}
