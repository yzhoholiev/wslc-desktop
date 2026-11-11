using Microsoft.UI.Xaml;
using WslcDesktop.Controls;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Views;

public sealed partial class VolumesPage
{
    public VolumesPage(AppServices services)
        : base(services)
    {
        ViewModel = new VolumesViewModel(services);
        InitializeComponent();
    }

    public VolumesViewModel ViewModel { get; }

    protected override IResourceListViewModel List => ViewModel;

    private async void OnRemoveClick(object sender, RoutedEventArgs e) => await RemoveAsync(sender.TagOf<VolumeItem>());

    private async void OnDeleteRequested(object? sender, object item) => await RemoveAsync((VolumeItem)item);

    private async Task RemoveAsync(VolumeItem item)
    {
        if (await Dialogs.ConfirmAsync(XamlRoot, "Delete volume?", $"Volume '{item.DisplayName}' and all of its data will be deleted.", "Delete"))
        {
            await ViewModel.RemoveAsync(item);
        }
    }

    private async void OnPruneAnonymousClick(object sender, RoutedEventArgs e)
    {
        if (await Dialogs.ConfirmAsync(XamlRoot, "Remove unused anonymous volumes?", "Anonymous volumes not attached to any container will be deleted.",
                "Remove"))
        {
            await ViewModel.PruneAsync(false);
        }
    }

    private async void OnPruneAllClick(object sender, RoutedEventArgs e)
    {
        if (await Dialogs.ConfirmAsync(XamlRoot, "Remove all unused volumes?",
                "Every volume not attached to a container, named ones included, will be deleted with its data.", "Remove"))
        {
            await ViewModel.PruneAsync(true);
        }
    }
}
