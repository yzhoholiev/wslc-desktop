using Microsoft.UI.Xaml;
using WslcDesktop.Controls;
using WslcDesktop.Models;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Views;

public sealed partial class DashboardPage
{
    private readonly AppServices _services;

    public DashboardPage(AppServices services)
    {
        _services = services;
        ViewModel = new DashboardViewModel(services);
        InitializeComponent();
        this.FollowMonitor(services.Monitor, OnRuntimeUpdated);
        Loaded += (_, _) => OnRuntimeUpdated();
    }

    public DashboardViewModel ViewModel { get; }

    private void OnRuntimeUpdated() => ViewModel.Apply(_services.Monitor.Latest);

    public async Task ShutdownWslAsync()
    {
        await this.WhenLoadedAsync();
        if (await Dialogs.ConfirmShutdownWslAsync(XamlRoot))
        {
            await ViewModel.ShutdownWslAsync();
        }
    }

    public async Task CleanupAsync()
    {
        await WhenReadyAsync();
        CleanupOptions? previous = _services.Settings.Current.LastCleanup;
        if (await CleanupDialog.ChooseAsync(XamlRoot, ViewModel.GetCleanupSummary(), previous) is { HasAny: true } options)
        {
            _services.Settings.Current.LastCleanup = options;
            _services.Settings.Save();
            await ViewModel.CleanupAsync(options);
        }
    }

    public async Task StopAllContainersAsync()
    {
        await WhenReadyAsync();
        if (await Dialogs.ConfirmStopAllContainersAsync(XamlRoot, _services.Monitor.Latest.RunningContainers))
        {
            await ViewModel.StopAllContainersAsync();
        }
    }

    public async Task StopRuntimeAsync()
    {
        await WhenReadyAsync();
        int running = _services.Monitor.Latest.RunningContainers;
        if (await Dialogs.ConfirmAsync(
                XamlRoot,
                "Stop the container runtime?",
                $"Session {Wslc.DefaultSessionName} shuts down and its VM is released. {Humanize.Count(running, "running container")} will be stopped. "
                + "Containers, images, volumes and networks are kept; the runtime starts again on the next container command.",
                "Stop runtime"))
        {
            await ViewModel.StopRuntimeAsync();
        }
    }

    /// <summary>
    ///     Waits for the page and for a first sample, since a jump-list command on a cold start arrives before either and its dialog would count nothing.
    /// </summary>
    private async Task WhenReadyAsync()
    {
        await this.WhenLoadedAsync();
        if (!_services.Monitor.HasSampled)
        {
            await _services.Monitor.RefreshAsync();
        }
    }

    private async void OnShutdownClick(object sender, RoutedEventArgs e) => await ShutdownWslAsync();

    private async void OnStopAllClick(object sender, RoutedEventArgs e) => await StopAllContainersAsync();

    private async void OnStopRuntimeClick(object sender, RoutedEventArgs e) => await StopRuntimeAsync();

    private async void OnCleanupClick(object sender, RoutedEventArgs e) => await CleanupAsync();

    private void OnStatCardClick(object sender, RoutedEventArgs e) => App.Window.ShowPage((string)((FrameworkElement)sender).Tag);
}
