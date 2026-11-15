using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Controls;

/// <summary>
///     Published ports as chips stacked one per line; TCP ports open in the browser.
/// </summary>
public sealed partial class PortChips : StackPanel
{
    private const int MaxChips = 2;

    public static readonly DependencyProperty PortsProperty = DependencyProperty.Register(
        nameof(Ports), typeof(string), typeof(PortChips), new PropertyMetadata(null, OnPortsChanged));

    public PortChips()
    {
        Orientation = Orientation.Vertical;
        Spacing = 2;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>
    ///     Ports in the CLI's notation, e.g. "127.0.0.1:8080->80/tcp, 53/udp".
    /// </summary>
    public string? Ports
    {
        get => (string?)GetValue(PortsProperty);
        set => SetValue(PortsProperty, value);
    }

    private static T Resource<T>(string key) => (T)Application.Current.Resources[key];

    private static void OnPortsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((PortChips)d).Rebuild();

    private static FrameworkElement CreateChip(PublishedPort port)
    {
        if (port.Url is not { } url)
        {
            return new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Style = Resource<Style>("NeutralPillStyle"),
                Child = Shortening(new TextBlock { Text = port.Label, Style = Resource<Style>("PillTextStyle") })
            };
        }

        var chip = new LinkButton
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = Resource<Style>("ChipButtonStyle"),
            Content = ShrinkingPair(
                Shortening(new TextBlock { Text = port.Label }),
                new FontIcon { Glyph = Glyphs.Open, FontSize = 9 })
        };

        ToolTipService.SetToolTip(chip, $"Open {url}");
        AutomationProperties.SetName(chip, $"Open {url}");
        chip.Click += (_, _) => App.Services.Shell.OpenUrl(url);
        return chip;
    }

    private static TextBlock Shortening(TextBlock text)
    {
        Overflow.SetShortens(text, true);
        return text;
    }

    private void Rebuild()
    {
        Children.Clear();
        PublishedPort[] ports = PublishedPort.ParseList(Ports ?? "");
        if (ports.Length == 0)
        {
            Children.Add(new TextBlock
            {
                Text = "—",
                Style = Resource<Style>("TertiaryTextStyle"),
                VerticalAlignment = VerticalAlignment.Center
            });

            return;
        }

        foreach (PublishedPort port in ports.Take(MaxChips - 1))
        {
            Children.Add(CreateChip(port));
        }

        if (ports.Length < MaxChips)
        {
            return;
        }

        FrameworkElement last = CreateChip(ports[MaxChips - 1]);
        if (ports.Length == MaxChips)
        {
            Children.Add(last);
            return;
        }

        // The overflow chip shares the last line, so a row never grows past two lines.
        Children.Add(ShrinkingPair(last, CreateOverflowChip(ports[MaxChips..])));
    }

    /// <summary>
    ///     Two elements side by side, where a narrow column shortens the first and keeps the second whole.
    /// </summary>
    private static Grid ShrinkingPair(FrameworkElement first, FrameworkElement second)
    {
        Grid.SetColumn(second, 1);
        return new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            ColumnSpacing = 4,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Children = { first, second }
        };
    }

    private static Button CreateOverflowChip(PublishedPort[] hidden)
    {
        var menu = new MenuFlyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
        foreach (PublishedPort port in hidden)
        {
            var item = new MenuFlyoutItem
            {
                Text = port.Description,
                IsEnabled = port.Url is not null,
                Icon = new FontIcon { Glyph = Glyphs.Open }
            };

            if (port.Url is { } url)
            {
                item.Click += (_, _) => App.Services.Shell.OpenUrl(url);
            }

            menu.Items.Add(item);
        }

        var more = new Button { Style = Resource<Style>("ChipButtonStyle"), Content = $"+{hidden.Length}", Flyout = menu };
        string label = $"Show {Humanize.Count(hidden.Length, "more port")}";
        ToolTipService.SetToolTip(more, label);
        AutomationProperties.SetName(more, label);
        return more;
    }
}
