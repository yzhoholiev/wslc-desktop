using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using WslcDesktop.Services;

namespace WslcDesktop.Controls;

/// <summary>
///     Area chart of recent percentage samples, newest on the right, with an optional second series drawn as a line on its own scale at the right.
/// </summary>
public sealed partial class Sparkline : Grid
{
    private const double MarkerSize = 7;

    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(object), typeof(Sparkline), new PropertyMetadata(null, OnValuesChanged));

    public static readonly DependencyProperty SecondaryValuesProperty = DependencyProperty.Register(
        nameof(SecondaryValues), typeof(object), typeof(Sparkline), new PropertyMetadata(null, OnValuesChanged));

    public static readonly DependencyProperty SecondaryTotalProperty = DependencyProperty.Register(
        nameof(SecondaryTotal), typeof(double), typeof(Sparkline), new PropertyMetadata(0d, OnValuesChanged));

    private static readonly double[] GridFractions = [0.25, 0.5, 0.75];
    private readonly Polygon _area = new();

    private readonly GradientStop _areaTop = new() { Offset = 0 };

    private readonly GradientStop _areaBottom = new() { Offset = 1 };

    private readonly Line[] _gridLines;
    private readonly Polyline _line = new();
    private readonly Polyline _secondaryLine = new();

    private readonly Ellipse _marker = new()
    {
        Width = MarkerSize,
        Height = MarkerSize,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Visibility = Visibility.Collapsed
    };

    private readonly TextBlock _ceilingLabel = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        IsHitTestVisible = false
    };

    private readonly TextBlock _secondaryCeilingLabel = new()
    {
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        IsHitTestVisible = false
    };

    private double _ceiling = 100;

    public Sparkline()
    {
        var gridLineStyle = (Style)Application.Current.Resources["ChartGridLineStyle"];
        _gridLines = [.. GridFractions.Select(_ => new Line { Style = gridLineStyle })];
        _ceilingLabel.Style = (Style)Application.Current.Resources["TertiaryCaptionStyle"];
        _secondaryCeilingLabel.Style = (Style)Application.Current.Resources["TertiaryCaptionStyle"];
        _line.Style = (Style)Application.Current.Resources["ChartLineStyle"];
        _secondaryLine.Style = (Style)Application.Current.Resources["ChartSecondaryLineStyle"];
        _marker.Style = (Style)Application.Current.Resources["ChartMarkerStyle"];
        _area.Fill = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = { _areaTop, _areaBottom }
        };

        foreach (Line line in _gridLines)
        {
            Children.Add(line);
        }

        Children.Add(_area);
        Children.Add(_secondaryLine);
        Children.Add(_line);
        Children.Add(_marker);
        Children.Add(_ceilingLabel);
        Children.Add(_secondaryCeilingLabel);
        SizeChanged += (_, _) => Redraw();
    }

    public object? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public object? SecondaryValues
    {
        get => GetValue(SecondaryValuesProperty);
        set => SetValue(SecondaryValuesProperty, value);
    }

    /// <summary>
    ///     The size in bytes that 100% of the second series stands for, which labels its scale; zero labels it in percent.
    /// </summary>
    public double SecondaryTotal
    {
        get => (double)GetValue(SecondaryTotalProperty);
        set => SetValue(SecondaryTotalProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Strokes reach just past the edges; reporting their extent would grow the chart on every redraw, a layout cycle.
        base.MeasureOverride(availableSize);
        return new Size(0, 0);
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Sparkline)d).Redraw();

    private double ToY(double value, double ceiling) => ActualHeight - Math.Clamp(value, 0, ceiling) / ceiling * ActualHeight;

    private void Redraw()
    {
        double[] secondary = SecondaryValues as double[] ?? [];
        _ceiling = ChartScale.PercentCeiling(Values as double[] ?? []);
        _ceilingLabel.Text = $"{_ceiling:0}%";

        // Each series fills the height on its own scale; one shared scale would flatten whichever is smaller.
        double secondaryCeiling = ChartScale.PercentCeiling(secondary);
        _secondaryCeilingLabel.Text = secondary.Length == 0
            ? ""
            : SecondaryTotal > 0
                ? ByteSize.Format((long)(SecondaryTotal * secondaryCeiling / 100))
                : $"{secondaryCeiling:0}%";

        // Each label takes its line's color to say whose scale it is; the lines' styles follow the theme.
        _ceilingLabel.Foreground = _line.Stroke;
        _secondaryCeilingLabel.Foreground = _secondaryLine.Stroke;

        for (int i = 0; i < GridFractions.Length; i++)
        {
            double y = ToY(GridFractions[i] * _ceiling, _ceiling);
            (_gridLines[i].X1, _gridLines[i].Y1, _gridLines[i].X2, _gridLines[i].Y2) = (0, y, ActualWidth, y);
        }

        // The fade takes the line's color, which the line's style updates on theme changes; a Color resource cannot be unboxed under Native AOT.
        if (_line.Stroke is SolidColorBrush { Color: var accent })
        {
            _areaTop.Color = Color.FromArgb(0x66, accent.R, accent.G, accent.B);
            _areaBottom.Color = Color.FromArgb(0x00, accent.R, accent.G, accent.B);
        }

        _line.Points.Clear();
        _secondaryLine.Points.Clear();
        _area.Points.Clear();
        _marker.Visibility = Visibility.Collapsed;

        if (Values is not double[] { Length: > 0 } values || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        Plot(_line, values, _ceiling);
        Plot(_secondaryLine, secondary, secondaryCeiling);
        double startX = _line.Points[0].X;

        _area.Points.Add(new Point(startX, ActualHeight));
        foreach (Point point in _line.Points)
        {
            _area.Points.Add(point);
        }

        _area.Points.Add(new Point(ActualWidth, ActualHeight));

        Point latest = _line.Points[^1];
        _marker.Margin = new Thickness(latest.X - MarkerSize / 2, latest.Y - MarkerSize / 2, 0, 0);
        _marker.Visibility = Visibility.Visible;
    }

    private void Plot(Polyline line, double[] values, double ceiling)
    {
        // The history holds a fixed number of samples, so a partly filled one grows in from the right.
        double step = ActualWidth / (MetricsHistory.Length - 1);
        double startX = ActualWidth - (values.Length - 1) * step;
        for (int i = 0; i < values.Length; i++)
        {
            line.Points.Add(new Point(startX + i * step, ToY(values[i], ceiling)));
        }
    }
}
