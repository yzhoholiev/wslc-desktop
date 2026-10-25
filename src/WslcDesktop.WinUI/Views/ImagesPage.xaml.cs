using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WslcDesktop.Controls;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Views;

public sealed partial class ImagesPage
{
    private readonly AppServices _services;

    public ImagesPage(AppServices services)
    {
        _services = services;
        ViewModel = new ImagesViewModel(services);
        InitializeComponent();
    }

    public ImagesViewModel ViewModel { get; }

    protected override IResourceListViewModel List => ViewModel;

    private void OnPullKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || !ViewModel.PullCommand.CanExecute(null))
        {
            return;
        }

        ViewModel.PullCommand.Execute(null);
        e.Handled = true;
    }

    private async void OnRunClick(object sender, RoutedEventArgs e)
    {
        if (await ViewModel.LoadRunDefaultsAsync(sender.TagOf<ImageItem>()) is not { } defaults
            || await RunImageDialog.ShowAsync(XamlRoot, defaults, _services.Commands.CreateContainerAsync) is not { } created)
        {
            return;
        }

        // Its logs show straight away whether it came up.
        if (created.Started)
        {
            App.Window.ShowContainer(created.Id);
        }

        await ViewModel.ReportCreatedAsync(defaults, created.Started);
    }

    private async void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        ImageItem item = sender.TagOf<ImageItem>();
        string message = item.IsUnused
            ? $"'{item.Reference}' will be deleted."
            : $"'{item.Reference}' is used by {Humanize.Count(item.ContainerCount, "container")}. It will be force-deleted anyway.";

        if (await Dialogs.ConfirmAsync(XamlRoot, "Delete image?", message, "Delete"))
        {
            await ViewModel.RemoveAsync(item, !item.IsUnused);
        }
    }

    private async void OnPruneDanglingClick(object sender, RoutedEventArgs e)
    {
        if (await Dialogs.ConfirmAsync(XamlRoot, "Remove dangling images?", "Untagged images that no container uses will be deleted.", "Remove"))
        {
            await ViewModel.PruneAsync(false);
        }
    }

    private async void OnPruneAllClick(object sender, RoutedEventArgs e)
    {
        if (await Dialogs.ConfirmAsync(XamlRoot, "Remove all unused images?", "Every image without a container will be deleted and must be pulled again.",
                "Remove"))
        {
            await ViewModel.PruneAsync(true);
        }
    }
}
