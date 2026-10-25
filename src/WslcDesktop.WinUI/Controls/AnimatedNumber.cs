using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WslcDesktop.Controls;

/// <summary>
///     Whole number that counts up or down to its new value.
/// </summary>
public sealed partial class AnimatedNumber : Grid
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(int), typeof(AnimatedNumber), new PropertyMetadata(-1, OnValueChanged));

    public static readonly DependencyProperty TextStyleProperty = DependencyProperty.Register(
        nameof(TextStyle), typeof(Style), typeof(AnimatedNumber), new PropertyMetadata(null, OnTextStyleChanged));

    private readonly TextBlock _text = new() { Text = "–" };
    private readonly Tween _tween;

    public AnimatedNumber()
    {
        Children.Add(_text);
        _tween = new Tween(value => _text.Text = Math.Round(value).ToString("N0"));
    }

    /// <summary>
    ///     The number to show, or a negative number to show a dash.
    /// </summary>
    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Style? TextStyle
    {
        get => (Style?)GetValue(TextStyleProperty);
        set => SetValue(TextStyleProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var number = (AnimatedNumber)d;
        if (number.Value < 0)
        {
            number._tween.To(double.NaN);
            number._text.Text = "–";
        }
        else
        {
            number._tween.To(number.Value);
        }
    }

    private static void OnTextStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AnimatedNumber)d)._text.Style = (Style?)e.NewValue;
}
