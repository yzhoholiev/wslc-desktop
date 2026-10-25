using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Specialized;
using System.ComponentModel;
using WslcDesktop.Controls;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Views;

/// <summary>
///     A resource list page's layout and wiring: search, toolbar, sortable rows, empty state and details pane, kept in step with the runtime.
/// </summary>
public sealed partial class ResourceListView
{
    public static readonly DependencyProperty ListProperty =
        DependencyProperty.Register(nameof(List), typeof(IResourceListViewModel), typeof(ResourceListView), new PropertyMetadata(null, OnListChanged));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ResourceListView), new PropertyMetadata(""));

    public static readonly DependencyProperty SearchPlaceholderProperty =
        DependencyProperty.Register(nameof(SearchPlaceholder), typeof(string), typeof(ResourceListView), new PropertyMetadata(""));

    public static readonly DependencyProperty ErrorTitleProperty =
        DependencyProperty.Register(nameof(ErrorTitle), typeof(string), typeof(ResourceListView), new PropertyMetadata(""));

    public static readonly DependencyProperty ToolbarProperty =
        DependencyProperty.Register(nameof(Toolbar), typeof(object), typeof(ResourceListView), new PropertyMetadata(null));

    public static readonly DependencyProperty ColumnHeadersProperty =
        DependencyProperty.Register(nameof(ColumnHeaders), typeof(object), typeof(ResourceListView), new PropertyMetadata(null));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(ResourceListView), new PropertyMetadata(null));

    private bool _isRestoringHighlight;

    public ResourceListView()
    {
        InitializeComponent();
        this.FollowMonitor(App.Services.Monitor, OnRuntimeUpdated);
        Loaded += async (_, _) =>
        {
            if (List is { } list)
            {
                await list.LoadAsync();
            }
        };

        Rows.SizeChanged += OnRowsSizeChanged;
    }

    public IResourceListViewModel? List
    {
        get => (IResourceListViewModel?)GetValue(ListProperty);
        set => SetValue(ListProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string SearchPlaceholder
    {
        get => (string)GetValue(SearchPlaceholderProperty);
        set => SetValue(SearchPlaceholderProperty, value);
    }

    /// <summary>
    ///     The title of the error bar shown when the list cannot be loaded.
    /// </summary>
    public string ErrorTitle
    {
        get => (string)GetValue(ErrorTitleProperty);
        set => SetValue(ErrorTitleProperty, value);
    }

    /// <summary>
    ///     The page's buttons, right-aligned beside the search box.
    /// </summary>
    public object? Toolbar
    {
        get => GetValue(ToolbarProperty);
        set => SetValue(ToolbarProperty, value);
    }

    /// <summary>
    ///     The sortable column headers, laid out on the same columns as <see cref="ItemTemplate" />.
    /// </summary>
    public object? ColumnHeaders
    {
        get => GetValue(ColumnHeadersProperty);
        set => SetValue(ColumnHeadersProperty, value);
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    private static void OnListChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (ResourceListView)d;
        if (e.OldValue is IResourceListViewModel old)
        {
            old.PropertyChanged -= view.OnListPropertyChanged;
            ((INotifyCollectionChanged)old.Items).CollectionChanged -= view.OnItemsChanged;
        }

        if (e.NewValue is IResourceListViewModel list)
        {
            list.PropertyChanged += view.OnListPropertyChanged;
            ((INotifyCollectionChanged)list.Items).CollectionChanged += view.OnItemsChanged;
        }
    }

    /// <summary>
    ///     Restores the row highlight, which the ListView drops when a reload, sort or search moves the selected item.
    /// </summary>
    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Deferred until the ListView has applied the change itself, and once for a whole reload.
        if (_isRestoringHighlight)
        {
            return;
        }

        _isRestoringHighlight = DispatcherQueue.TryEnqueue(() =>
        {
            _isRestoringHighlight = false;
            if (List is { SelectedItem: { } selected } && !Equals(Rows.SelectedItem, selected))
            {
                Rows.SelectedItem = selected;
            }
        });
    }

    private void OnListPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (List is not { } list)
        {
            return;
        }

        if (e.PropertyName != nameof(ListViewModel<>.Selected))
        {
            return;
        }

        Rows.SelectedItem = list.SelectedItem;
        if (list.SelectedItem is { } selected)
        {
            Rows.ScrollIntoView(selected);
        }
    }

    /// <summary>
    ///     Keeps the selected row in view when the list is first laid out or gets shorter, as it does when the details pane opens below it.
    /// </summary>
    private void OnRowsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if ((e.PreviousSize.Height == 0 || e.NewSize.Height < e.PreviousSize.Height) && List?.SelectedItem is { } selected)
        {
            Rows.ScrollIntoView(selected);
        }
    }

    private void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (List is { } list)
        {
            list.SelectedItem = e.ClickedItem;
        }
    }

    /// <summary>
    ///     Follows the keyboard, which moves the selection without a click.
    /// </summary>
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Ignore deselection caused by list reordering; closing the details pane clears the selection explicitly.
        if (List is { } list && e.AddedItems.Count > 0 && !Equals(e.AddedItems[0], list.SelectedItem))
        {
            list.SelectedItem = e.AddedItems[0];
        }
    }

    private void OnDetailsCloseRequested(object? sender, EventArgs e) => List?.CloseDetails();

    private async void OnEmptyActionClick(object? sender, RoutedEventArgs e)
    {
        if (List is { } list)
        {
            await list.RunEmptyActionAsync();
        }
    }

    private void OnRuntimeUpdated() => List?.OnRuntimeUpdated(App.Services.Monitor.Latest);
}
