using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace WslcDesktop.Controls;

/// <summary>
///     An editable list of two-field rows, such as port or volume mappings.
/// </summary>
internal sealed class PairRows
{
    private readonly Func<Task<string?>>? _browse;
    private readonly List<(TextBox First, TextBox Second)> _entries = [];
    private readonly string _firstPlaceholder;
    private readonly StackPanel _rows = new() { Spacing = 8 };
    private readonly string _secondPlaceholder;
    private readonly string _separator;

    /// <summary>
    ///     Starts with no rows and an Add link below them.
    /// </summary>
    /// <param name="addText">The text of the link that adds an empty row.</param>
    /// <param name="firstPlaceholder">Placeholder and accessible name of each row's first field.</param>
    /// <param name="separator">Shown between the two fields, such as an arrow or an equals sign.</param>
    /// <param name="secondPlaceholder">Placeholder and accessible name of each row's second field.</param>
    /// <param name="browse">When set, a browse button beside the first field fills it with what this returns.</param>
    public PairRows(string addText, string firstPlaceholder, string separator, string secondPlaceholder, Func<Task<string?>>? browse = null)
    {
        _firstPlaceholder = firstPlaceholder;
        _separator = separator;
        _secondPlaceholder = secondPlaceholder;
        _browse = browse;

        var add = new HyperlinkButton { Content = addText, Padding = new Thickness(0) };
        add.Click += (_, _) => Add("", "");
        Panel.Children.Add(_rows);
        Panel.Children.Add(add);
    }

    public StackPanel Panel { get; } = new() { Spacing = 8 };

    public IEnumerable<(string First, string Second)> Values => _entries.Select(entry => (entry.First.Text, entry.Second.Text));

    public void Add(string first, string second)
    {
        var firstBox = new TextBox { PlaceholderText = _firstPlaceholder, Text = first };
        var secondBox = new TextBox { PlaceholderText = _secondPlaceholder, Text = second };
        AutomationProperties.SetName(firstBox, _firstPlaceholder);
        AutomationProperties.SetName(secondBox, _secondPlaceholder);

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(firstBox);
        if (_browse is not null)
        {
            Button browse = FormParts.BrowseButton(firstBox, _browse);
            Grid.SetColumn(browse, 1);
            row.Children.Add(browse);
        }

        var separator = new TextBlock { Text = _separator, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(separator, 2);
        row.Children.Add(separator);
        Grid.SetColumn(secondBox, 3);
        row.Children.Add(secondBox);

        var remove = new Button
        {
            Content = new FontIcon { Glyph = Glyphs.Remove, FontSize = 14 },
            Style = (Style)Application.Current.Resources["IconButtonStyle"]
        };

        ToolTipService.SetToolTip(remove, "Remove");
        AutomationProperties.SetName(remove, "Remove");
        Grid.SetColumn(remove, 4);
        row.Children.Add(remove);

        (TextBox firstBox, TextBox secondBox) entry = (firstBox, secondBox);
        remove.Click += (_, _) =>
        {
            _entries.Remove(entry);
            _rows.Children.Remove(row);
        };

        _entries.Add(entry);
        _rows.Children.Add(row);
    }
}
