using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WslcDesktop.Controls;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Views;

/// <summary>
///     Page that shows one resource list in a <see cref="ResourceListView" />, whose rows' usage chips list the containers using them.
/// </summary>
public abstract class ResourceListPage : Page
{
    protected abstract IResourceListViewModel List { get; }

    protected void OnUsageClick(object sender, RoutedEventArgs e) =>
        UsageFlyout.ShowAt((FrameworkElement)sender, List.ContainersUsing(sender.TagOf<object>()));
}
