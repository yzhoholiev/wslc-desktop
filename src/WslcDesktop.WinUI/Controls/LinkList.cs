using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WslcDesktop.Controls;

/// <summary>
///     Comma-separated names shown as links in a row.
/// </summary>
public sealed partial class LinkList : StackPanel
{
    public static readonly DependencyProperty NamesProperty = DependencyProperty.Register(
        nameof(Names), typeof(string), typeof(LinkList), new PropertyMetadata(null, OnNamesChanged));

    public LinkList()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
    }

    public string? Names
    {
        get => (string?)GetValue(NamesProperty);
        set => SetValue(NamesProperty, value);
    }

    public event EventHandler<string>? LinkClick;

    private static void OnNamesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LinkList)d).Rebuild();

    private void Rebuild()
    {
        Children.Clear();
        foreach (string name in (Names ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var link = new Button
            {
                Style = (Style)Application.Current.Resources["ChipButtonStyle"],
                Content = name
            };

            ToolTipService.SetToolTip(link, $"Open {name}");
            link.Click += (_, _) => LinkClick?.Invoke(this, name);
            Children.Add(link);
        }
    }
}
