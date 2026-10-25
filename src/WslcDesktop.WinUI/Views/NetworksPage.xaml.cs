using Microsoft.UI.Xaml;
using WslcDesktop.Controls;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Views;

public sealed partial class NetworksPage
{
    public NetworksPage(AppServices services)
    {
        ViewModel = new NetworksViewModel(services);
        InitializeComponent();
    }

    public NetworksViewModel ViewModel { get; }

    protected override IResourceListViewModel List => ViewModel;

    private async void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        NetworkItem item = sender.TagOf<NetworkItem>();
        if (await Dialogs.ConfirmAsync(XamlRoot, "Delete network?", $"Network '{item.Name}' will be deleted.", "Delete"))
        {
            await ViewModel.RemoveAsync(item);
        }
    }

    private async void OnPruneClick(object sender, RoutedEventArgs e)
    {
        if (await Dialogs.ConfirmAsync(XamlRoot, "Remove unused networks?", "User-defined networks without containers will be deleted.", "Remove"))
        {
            await ViewModel.PruneAsync();
        }
    }
}
