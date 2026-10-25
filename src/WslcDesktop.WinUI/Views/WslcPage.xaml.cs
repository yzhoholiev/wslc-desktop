using Microsoft.UI.Xaml;
using WslcDesktop.Controls;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Views;

public sealed partial class WslcPage
{
    private readonly AppServices _services;

    public WslcPage(AppServices services)
    {
        _services = services;
        ViewModel = new WslcViewModel(services);
        InitializeComponent();
        this.FollowMonitor(services.Monitor, OnRuntimeUpdated);
        Loaded += (_, _) => ViewModel.Load();
    }

    public WslcViewModel ViewModel { get; }

    private void OnRuntimeUpdated() => ViewModel.Apply(_services.Monitor.Latest);

    private void OnSessionShellClick(object sender, RoutedEventArgs e) => ViewModel.OpenSessionShell(sender.TagOf<WslcSessionItem>());

    private async void OnTerminateSessionClick(object sender, RoutedEventArgs e)
    {
        WslcSessionItem session = sender.TagOf<WslcSessionItem>();
        if (await Dialogs.ConfirmAsync(XamlRoot, $"Terminate session {session.Name}?", "All containers running in this session will be stopped.", "Terminate"))
        {
            await ViewModel.TerminateSessionAsync(session);
        }
    }
}
