using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WslcDesktop.Controls;

/// <summary>
///     Centered placeholder for an empty list: an icon, a title, an explanation and an optional action.
/// </summary>
public sealed partial class EmptyState : StackPanel
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(EmptyState), new PropertyMetadata("", OnContentChanged));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(EmptyState), new PropertyMetadata("", OnContentChanged));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(EmptyState), new PropertyMetadata("", OnContentChanged));

    public static readonly DependencyProperty ActionTextProperty = DependencyProperty.Register(
        nameof(ActionText), typeof(string), typeof(EmptyState), new PropertyMetadata("", OnContentChanged));

    public static readonly DependencyProperty IsActionEnabledProperty = DependencyProperty.Register(
        nameof(IsActionEnabled), typeof(bool), typeof(EmptyState), new PropertyMetadata(true, OnContentChanged));

    private readonly Button _action = new() { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };

    private readonly FontIcon _icon = new() { FontSize = 40, Margin = new Thickness(0, 0, 0, 4) };

    private readonly TextBlock _message = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 380
    };

    private readonly TextBlock _title = new() { HorizontalAlignment = HorizontalAlignment.Center };

    public EmptyState()
    {
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        Spacing = 4;
        Padding = new Thickness(24);

        _icon.Style = (Style)Application.Current.Resources["TertiaryIconStyle"];
        _title.Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"];
        _message.Style = (Style)Application.Current.Resources["SecondaryTextStyle"];
        _action.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        _action.Click += (_, e) => ActionClick?.Invoke(this, e);

        Children.Add(_icon);
        Children.Add(_title);
        Children.Add(_message);
        Children.Add(_action);
        Update();
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public bool IsActionEnabled
    {
        get => (bool)GetValue(IsActionEnabledProperty);
        set => SetValue(IsActionEnabledProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? ActionClick;

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((EmptyState)d).Update();

    private void Update()
    {
        _icon.Glyph = Glyph;
        _title.Text = Title;
        _message.Text = Message;
        _message.Visibility = string.IsNullOrEmpty(Message) ? Visibility.Collapsed : Visibility.Visible;
        _action.Content = ActionText;
        _action.Visibility = string.IsNullOrEmpty(ActionText) ? Visibility.Collapsed : Visibility.Visible;
        _action.IsEnabled = IsActionEnabled;
    }
}
