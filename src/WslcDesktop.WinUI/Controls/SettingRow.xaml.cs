using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace WslcDesktop.Controls;

/// <summary>
///     A settings card with a title and description on the left and the control that changes the setting on the right.
/// </summary>
[ContentProperty(Name = nameof(Action))]
public sealed partial class SettingRow
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingRow), new PropertyMetadata(""));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingRow), new PropertyMetadata(""));

    public static readonly DependencyProperty ActionProperty =
        DependencyProperty.Register(nameof(Action), typeof(object), typeof(SettingRow), new PropertyMetadata(null));

    public SettingRow()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    ///     The control that changes the setting, such as a toggle; the row's content in XAML.
    /// </summary>
    public object? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }
}
