using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WslcDesktop.Controls;

/// <summary>
///     Selectable monospace text that derived views color by its content.
/// </summary>
public abstract class MonospaceView : Grid
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(MonospaceView), new PropertyMetadata("", OnRenderInputChanged));

    private bool _isWatchingContrast;

    protected MonospaceView()
    {
        Children.Add(Output);
        ActualThemeChanged += (_, _) => Render();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected TextBlock Output { get; } = new()
    {
        FontFamily = Fonts.Mono,
        FontSize = 12,
        IsTextSelectionEnabled = true
    };

    protected bool IsLightTheme => ActualTheme == ElementTheme.Light;

    /// <summary>
    ///     Whether a contrast theme is on, whose colors the user chose and content coloring would override.
    /// </summary>
    protected static bool IsHighContrast => ContrastTheme.IsOn;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // A contrast theme can switch on without changing ActualTheme, also while a cached page is off-screen.
        if (!_isWatchingContrast)
        {
            ContrastTheme.Changed += OnContrastChanged;
            _isWatchingContrast = true;
        }

        Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // WinUI can raise Unloaded after the element was loaded again, as on a quick navigation back.
        if (_isWatchingContrast && !IsLoaded)
        {
            ContrastTheme.Changed -= OnContrastChanged;
            _isWatchingContrast = false;
        }
    }

    private void OnContrastChanged() => DispatcherQueue.TryEnqueue(() => Render());

    /// <summary>
    ///     A property-changed callback for derived properties that also change how the text renders.
    /// </summary>
    protected static void OnRenderInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MonospaceView)d).Render();

    /// <summary>
    ///     Replaces the inlines of <see cref="Output" /> with <see cref="Text" />, colored for the current theme.
    /// </summary>
    protected abstract void Render();
}
