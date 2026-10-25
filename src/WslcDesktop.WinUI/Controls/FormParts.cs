using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;
using WslcDesktop.Services;

namespace WslcDesktop.Controls;

/// <summary>
///     Building blocks for forms built in code: labelled inputs with help bubbles, sections, pairs and browse buttons.
/// </summary>
internal static class FormParts
{
    /// <summary>
    ///     Labels an input above it, with a help bubble that explains the option.
    /// </summary>
    public static T Field<T>(T input, string label, string help) where T : Control
    {
        AutomationProperties.SetName(input, label);
        switch (input)
        {
            case TextBox box:
                box.Header = Label(new TextBlock { Text = label }, help);
                break;
            case ComboBox comboBox:
                comboBox.Header = Label(new TextBlock { Text = label }, help);
                break;
        }

        return input;
    }

    /// <summary>
    ///     The element followed by a help bubble that explains it.
    /// </summary>
    public static StackPanel Label(FrameworkElement element, string help)
    {
        element.VerticalAlignment = VerticalAlignment.Center;
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        label.Children.Add(element);
        label.Children.Add(HelpButton(help));
        return label;
    }

    public static StackPanel Section(string title, string help, string? hint, UIElement body)
    {
        var section = new StackPanel { Spacing = 6, Margin = new Thickness(0, 4, 0, 0) };
        section.Children.Add(Label(new TextBlock { Text = title, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] }, help));
        if (hint is not null)
        {
            section.Children.Add(new TextBlock
            {
                Text = hint,
                TextWrapping = TextWrapping.Wrap,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
        }

        section.Children.Add(body);
        return section;
    }

    public static Grid Pair(FrameworkElement first, FrameworkElement second)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(second, 1);
        grid.Children.Add(first);
        grid.Children.Add(second);
        return grid;
    }

    public static Grid WithBrowse(TextBox box, Func<Task<string?>> pick)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Button browse = BrowseButton(box, pick);
        browse.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetColumn(browse, 1);
        grid.Children.Add(box);
        grid.Children.Add(browse);
        return grid;
    }

    /// <summary>
    ///     A button that fills the text box with what the picker returns.
    /// </summary>
    public static Button BrowseButton(TextBox target, Func<Task<string?>> pick)
    {
        var button = new Button
        {
            Content = new FontIcon { Glyph = Glyphs.Browse, FontSize = 14 },
            Style = (Style)Application.Current.Resources["IconButtonStyle"]
        };

        ToolTipService.SetToolTip(button, "Browse");
        AutomationProperties.SetName(button, "Browse");
        button.Click += async (_, _) =>
        {
            try
            {
                if (await pick() is { } path)
                {
                    target.Text = path;
                }
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
            }
        };

        return button;
    }

    public static async Task<string?> PickFolderAsync() => (await new FolderPicker(App.Window.AppWindow.Id).PickSingleFolderAsync())?.Path;

    public static async Task<string?> PickFileAsync() => (await new FileOpenPicker(App.Window.AppWindow.Id).PickSingleFileAsync())?.Path;

    /// <summary>
    ///     A (?) that shows the explanation on hover, and pins it in a flyout on a click or tap.
    /// </summary>
    private static Button HelpButton(string text)
    {
        var button = new Button
        {
            Content = new FontIcon { Glyph = Glyphs.Help, FontSize = 12 },
            Padding = new Thickness(4),
            MinWidth = 0,
            MinHeight = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Flyout = new Flyout { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 } }
        };

        ToolTipService.SetToolTip(button, text);
        AutomationProperties.SetName(button, "Help");
        AutomationProperties.SetHelpText(button, text);
        return button;
    }
}
