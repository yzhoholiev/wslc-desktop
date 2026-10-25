using Microsoft.UI.Xaml;
using WslcDesktop.Controls;
using WslcDesktop.Models;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Views;

public sealed partial class WslPage
{
    public WslPage(AppServices services)
    {
        ViewModel = new WslViewModel(services);
        InitializeComponent();
        this.FollowMonitor(services.Monitor, OnRuntimeUpdated);
        Loaded += async (_, _) => await ViewModel.LoadAsync();
    }

    public WslViewModel ViewModel { get; }

    private async void OnRuntimeUpdated() => await ViewModel.OnRuntimeUpdatedAsync();

    private async void OnShutdownClick(object sender, RoutedEventArgs e)
    {
        if (await Dialogs.ConfirmShutdownWslAsync(XamlRoot))
        {
            await ViewModel.ShutdownAsync();
        }
    }

    private void OnDistroTerminalClick(object sender, RoutedEventArgs e) => ViewModel.OpenDistroTerminal(sender.TagOf<WslDistro>());

    private async void OnSetDefaultClick(object sender, RoutedEventArgs e) => await ViewModel.SetDefaultAsync(sender.TagOf<WslDistro>());

    private async void OnTerminateDistroClick(object sender, RoutedEventArgs e)
    {
        WslDistro distro = sender.TagOf<WslDistro>();
        if (await Dialogs.ConfirmAsync(XamlRoot, $"Terminate {distro.Name}?", "All processes running in this distribution will be stopped.", "Terminate"))
        {
            await ViewModel.TerminateAsync(distro);
        }
    }
}
