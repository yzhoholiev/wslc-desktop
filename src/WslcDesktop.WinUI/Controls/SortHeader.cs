using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Controls;

/// <summary>
///     Column header that sorts its list when clicked and shows the current sort direction.
/// </summary>
public sealed partial class SortHeader : Button
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SortHeader), new PropertyMetadata("", OnStateChanged));

    public static readonly DependencyProperty KeyProperty = DependencyProperty.Register(
        nameof(Key), typeof(string), typeof(SortHeader), new PropertyMetadata("", OnStateChanged));

    public static readonly DependencyProperty SortProperty = DependencyProperty.Register(
        nameof(Sort), typeof(ListSort), typeof(SortHeader), new PropertyMetadata(null, OnSortChanged));

    private readonly FontIcon _arrow = new() { FontSize = 9, VerticalAlignment = VerticalAlignment.Center };

    private readonly TextBlock _label = new();

    private ListSort? _watched;

    public SortHeader()
    {
        Style = (Style)Application.Current.Resources["SortHeaderButtonStyle"];
        _label.Style = (Style)Application.Current.Resources["ColumnHeaderStyle"];
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _label, _arrow } };
        Update();

        // Watching only while loaded keeps a page dropped to the tray free of references from its view model.
        Loaded += (_, _) =>
        {
            Watch(Sort);
            Update();
        };

        Unloaded += (_, _) => Watch(null);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    ///     The column this header sorts by; also the command parameter.
    /// </summary>
    public string Key
    {
        get => (string)GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    /// <summary>
    ///     The sort of the list this header belongs to, which it shows and changes.
    /// </summary>
    public ListSort? Sort
    {
        get => (ListSort?)GetValue(SortProperty);
        set => SetValue(SortProperty, value);
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((SortHeader)d).Update();

    private static void OnSortChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var header = (SortHeader)d;
        if (header.IsLoaded)
        {
            header.Watch(e.NewValue as ListSort);
        }

        header.Command = header.Sort?.ChangeCommand;
        header.Update();
    }

    private void OnSortPropertyChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void Watch(ListSort? sort)
    {
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnSortPropertyChanged;
        }

        _watched = sort;
        if (sort is not null)
        {
            sort.PropertyChanged += OnSortPropertyChanged;
        }
    }

    private void Update()
    {
        bool isActive = Key.Length > 0 && Key == Sort?.Key;
        bool isDescending = Sort?.IsDescending == true;
        _label.Text = Text;
        _arrow.Glyph = isDescending ? Glyphs.SortDescending : Glyphs.SortAscending;
        _arrow.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        CommandParameter = Key;
        AutomationProperties.SetName(this, isActive ? $"{Text}, sorted {(isDescending ? "descending" : "ascending")}" : $"Sort by {Text}");
    }
}
