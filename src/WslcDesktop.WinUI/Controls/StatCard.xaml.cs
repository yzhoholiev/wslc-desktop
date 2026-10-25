using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace WslcDesktop.Controls;

/// <summary>
///     A dashboard card that counts one kind of resource and opens its page when clicked.
/// </summary>
public sealed partial class StatCard
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatCard), new PropertyMetadata(""));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(StatCard), new PropertyMetadata(""));

    public static readonly DependencyProperty BadgeBrushProperty =
        DependencyProperty.Register(nameof(BadgeBrush), typeof(Brush), typeof(StatCard), new PropertyMetadata(null));

    public static readonly DependencyProperty BadgeBackgroundProperty =
        DependencyProperty.Register(nameof(BadgeBackground), typeof(Brush), typeof(StatCard), new PropertyMetadata(null));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(int), typeof(StatCard), new PropertyMetadata(-1));

    public static readonly DependencyProperty DetailProperty =
        DependencyProperty.Register(nameof(Detail), typeof(string), typeof(StatCard), new PropertyMetadata(""));

    public static readonly DependencyProperty DotStyleProperty =
        DependencyProperty.Register(nameof(DotStyle), typeof(Style), typeof(StatCard), new PropertyMetadata(null));

    public static readonly DependencyProperty ShowDotProperty =
        DependencyProperty.Register(nameof(ShowDot), typeof(bool), typeof(StatCard), new PropertyMetadata(false));

    public StatCard()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public Brush? BadgeBrush
    {
        get => (Brush?)GetValue(BadgeBrushProperty);
        set => SetValue(BadgeBrushProperty, value);
    }

    public Brush? BadgeBackground
    {
        get => (Brush?)GetValue(BadgeBackgroundProperty);
        set => SetValue(BadgeBackgroundProperty, value);
    }

    /// <summary>
    ///     The count, or a negative number while it is unknown.
    /// </summary>
    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Detail
    {
        get => (string)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>
    ///     The style of the status dot before the detail, shown when <see cref="ShowDot" /> is set.
    /// </summary>
    public Style? DotStyle
    {
        get => (Style?)GetValue(DotStyleProperty);
        set => SetValue(DotStyleProperty, value);
    }

    public bool ShowDot
    {
        get => (bool)GetValue(ShowDotProperty);
        set => SetValue(ShowDotProperty, value);
    }
}
