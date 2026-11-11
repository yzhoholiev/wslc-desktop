using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WslcDesktop.Controls;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Views;

/// <summary>
///     Page that shows one resource list in a <see cref="ResourceListView" />, whose rows' usage chips list the containers using them.
/// </summary>
public abstract class ResourceListPage(AppServices services) : Page
{
    protected AppServices Services { get; } = services;

    protected abstract IResourceListViewModel List { get; }

    /// <summary>
    ///     Copies the text a menu item carries in its Tag.
    /// </summary>
    protected void OnCopyClick(object sender, RoutedEventArgs e) => ClipboardText.Copy(sender.TagOf<string>(), Services.Notifier);

    protected void OnUsageClick(object sender, RoutedEventArgs e) =>
        UsageFlyout.ShowAt((FrameworkElement)sender, List.ContainersUsing(sender.TagOf<object>()));
}
