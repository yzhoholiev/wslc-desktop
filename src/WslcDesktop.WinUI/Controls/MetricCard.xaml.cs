using Microsoft.UI.Xaml;

namespace WslcDesktop.Controls;

/// <summary>
///     A dashboard card with a current value, a caption and a graph of recent samples.
/// </summary>
public sealed partial class MetricCard
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(MetricCard), new PropertyMetadata(""));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(MetricCard), new PropertyMetadata(""));

    public static readonly DependencyProperty CaptionProperty =
        DependencyProperty.Register(nameof(Caption), typeof(string), typeof(MetricCard), new PropertyMetadata(""));

    public static readonly DependencyProperty HistoryProperty =
        DependencyProperty.Register(nameof(History), typeof(object), typeof(MetricCard), new PropertyMetadata(null));

    public static readonly DependencyProperty HasHistoryProperty =
        DependencyProperty.Register(nameof(HasHistory), typeof(bool), typeof(MetricCard), new PropertyMetadata(false));

    public MetricCard()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>
    ///     The samples to graph, as a double array; see <see cref="Sparkline.Values" />.
    /// </summary>
    public object? History
    {
        get => GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    /// <summary>
    ///     Whether there are enough samples to graph.
    /// </summary>
    public bool HasHistory
    {
        get => (bool)GetValue(HasHistoryProperty);
        set => SetValue(HasHistoryProperty, value);
    }
}
