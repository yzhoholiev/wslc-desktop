using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;

namespace WslcDesktop.Controls;

/// <summary>
///     Details pane showing a resource's title and its raw inspect output.
/// </summary>
public sealed partial class InspectPanel
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(InspectPanel), new PropertyMetadata(""));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(InspectPanel), new PropertyMetadata(""));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(InspectPanel), new PropertyMetadata("", OnTextChanged));

    public InspectPanel()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public event EventHandler? CloseRequested;

    /// <summary>
    ///     Scrolls back to the top, since a ScrollViewer keeps its offset when the content is swapped.
    /// </summary>
    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((InspectPanel)d).BodyScroller.ChangeView(0, 0, null, true);

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(Text);
        try
        {
            Clipboard.SetContent(package);
        }
        catch (COMException ex)
        {
            // Another process holding the clipboard open makes the call fail.
            App.Services.Notifier.Error("Copy failed", ex.Message);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
