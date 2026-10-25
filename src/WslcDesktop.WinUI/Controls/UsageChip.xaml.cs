using Microsoft.UI.Xaml;

namespace WslcDesktop.Controls;

/// <summary>
///     How many containers use a resource, as a chip that lists them when clicked, or "Unused".
/// </summary>
public sealed partial class UsageChip
{
    public static readonly DependencyProperty ContainerCountProperty =
        DependencyProperty.Register(nameof(ContainerCount), typeof(int), typeof(UsageChip), new PropertyMetadata(0, OnContainerCountChanged));

    public UsageChip()
    {
        InitializeComponent();
    }

    public int ContainerCount
    {
        get => (int)GetValue(ContainerCountProperty);
        set => SetValue(ContainerCountProperty, value);
    }

    public bool IsUnused => ContainerCount == 0;

    /// <summary>
    ///     Raised by the chip itself, so handlers can read its <see cref="FrameworkElement.Tag" /> and show a flyout at it.
    /// </summary>
    public event RoutedEventHandler? Click;

    private static void OnContainerCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((UsageChip)d).Bindings.Update();

    private void OnButtonClick(object sender, RoutedEventArgs e) => Click?.Invoke(this, e);
}
