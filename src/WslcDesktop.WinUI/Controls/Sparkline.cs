using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using WslcDesktop.Services;

namespace WslcDesktop.Controls;

/// <summary>
///     Area chart of recent percentage samples, newest on the right.
/// </summary>
public sealed partial class Sparkline : Grid
{
    private const double MarkerSize = 7;

    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(object), typeof(Sparkline), new PropertyMetadata(null, OnValuesChanged));

    private static readonly double[] GridFractions = [0.25, 0.5, 0.75];
    private readonly Polygon _area = new();

    private readonly GradientStop _areaTop = new() { Offset = 0 };

    private readonly GradientStop _areaBottom = new() { Offset = 1 };

    private readonly Line[] _gridLines;
    private readonly Polyline _line = new();

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

    private double _ceiling = 100;

    public Sparkline()
    {
        var gridLineStyle = (Style)Application.Current.Resources["ChartGridLineStyle"];
        _gridLines = [.. GridFractions.Select(_ => new Line { Style = gridLineStyle })];
        _ceilingLabel.Style = (Style)Application.Current.Resources["TertiaryCaptionStyle"];
        _line.Style = (Style)Application.Current.Resources["ChartLineStyle"];
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
        Children.Add(_line);
        Children.Add(_marker);
        Children.Add(_ceilingLabel);
        SizeChanged += (_, _) => Redraw();
    }

    public object? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Strokes reach just past the edges; reporting their extent would grow the chart on every redraw, a layout cycle.
        base.MeasureOverride(availableSize);
        return new Size(0, 0);
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Sparkline)d).Redraw();

    private double ToY(double value) => ActualHeight - Math.Clamp(value, 0, _ceiling) / _ceiling * ActualHeight;

    private void Redraw()
    {
        _ceiling = ChartScale.PercentCeiling(Values as double[] ?? []);
        _ceilingLabel.Text = $"{_ceiling:0}%";
        for (int i = 0; i < GridFractions.Length; i++)
        {
            double y = ToY(GridFractions[i] * _ceiling);
            (_gridLines[i].X1, _gridLines[i].Y1, _gridLines[i].X2, _gridLines[i].Y2) = (0, y, ActualWidth, y);
        }

        // The fade takes the line's color, which the line's style updates on theme changes; a Color resource cannot be unboxed under Native AOT.
        if (_line.Stroke is SolidColorBrush { Color: var accent })
        {
            _areaTop.Color = Color.FromArgb(0x66, accent.R, accent.G, accent.B);
            _areaBottom.Color = Color.FromArgb(0x00, accent.R, accent.G, accent.B);
        }

        _line.Points.Clear();
        _area.Points.Clear();
        _marker.Visibility = Visibility.Collapsed;

        if (Values is not double[] { Length: > 0 } values || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        // The history holds a fixed number of samples, so a partly filled one grows in from the right.
        double step = ActualWidth / (MetricsHistory.Length - 1);
        double startX = ActualWidth - (values.Length - 1) * step;

        for (int i = 0; i < values.Length; i++)
        {
            _line.Points.Add(new Point(startX + i * step, ToY(values[i])));
        }

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
}
