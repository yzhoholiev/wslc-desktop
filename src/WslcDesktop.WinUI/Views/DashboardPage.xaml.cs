using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WslcDesktop.Controls;
using WslcDesktop.Models;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Views;

public sealed partial class DashboardPage
{
    private static readonly CleanupOptions UnusedImagesOnly = new(false, false, false, true, true, false, false);

    private readonly AppServices _services;

    public DashboardPage(AppServices services)
    {
        _services = services;
        ViewModel = new DashboardViewModel(services);
        InitializeComponent();
        this.FollowMonitor(services.Monitor, OnRuntimeUpdated);
        Loaded += (_, _) => OnRuntimeUpdated();

        // A ScrollViewer gives its content unbounded height; this lets the last row fill the window and still scroll when it is short.
        Scroller.SizeChanged += (_, e) => PageContent.MinHeight = e.NewSize.Height;
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

    public Task CleanupAsync() => CleanupAsync(null);

    /// <summary>
    ///     Asks what to clean up and does it.
    /// </summary>
    /// <param name="preset">What the dialog starts with for a cleanup of one kind of thing; the choice made last time otherwise.</param>
    private async Task CleanupAsync(CleanupOptions? preset)
    {
        if (!await WhenReadyAsync())
        {
            return;
        }

        CleanupOptions? initial = preset ?? _services.Settings.Current.LastCleanup;
        if (await CleanupDialog.ChooseAsync(XamlRoot, ViewModel.GetCleanupSummary(), initial) is { HasAny: true } options)
        {
            // A cleanup narrowed to one kind is not what the user wants offered next time.
            if (preset is null)
            {
                _services.Settings.Current.LastCleanup = options;
                _services.Settings.Save();
            }

            await ViewModel.CleanupAsync(options);
        }
    }

    public async Task StopAllContainersAsync()
    {
        if (!await WhenReadyAsync())
        {
            return;
        }

        if (await Dialogs.ConfirmStopAllContainersAsync(XamlRoot, _services.Monitor.Latest.RunningContainers))
        {
            await ViewModel.StopAllContainersAsync();
        }
    }

    public async Task StopRuntimeAsync()
    {
        if (!await WhenReadyAsync())
        {
            return;
        }

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
    /// <returns>False when the page left the window meanwhile, which leaves a dialog nowhere to open.</returns>
    private async Task<bool> WhenReadyAsync()
    {
        await this.WhenLoadedAsync();
        if (!_services.Monitor.HasSampled)
        {
            await _services.Monitor.RefreshAsync();
        }

        return XamlRoot is not null;
    }

    private async void OnShutdownClick(object sender, RoutedEventArgs e) => await ShutdownWslAsync();

    private async void OnStopAllClick(object sender, RoutedEventArgs e) => await StopAllContainersAsync();

    private async void OnStopRuntimeClick(object sender, RoutedEventArgs e) => await StopRuntimeAsync();

    private async void OnCleanupClick(object sender, RoutedEventArgs e) => await CleanupAsync();

    private void OnStatCardClick(object sender, RoutedEventArgs e) => App.Window.ShowPage((string)((FrameworkElement)sender).Tag);

    private void OnEndpointContainerClick(object sender, RoutedEventArgs e) => App.Window.ShowContainer(sender.TagOf<string>());

    private async void OnAttentionPrimaryClick(object sender, RoutedEventArgs e) => await RunPrimaryAsync(sender.TagOf<AttentionItem>());

    private async void OnAttentionSecondaryClick(object sender, RoutedEventArgs e) => await ViewModel.StartOrStopAsync(sender.TagOf<AttentionItem>());

    private void OnAttentionDismissClick(object sender, RoutedEventArgs e) => ViewModel.Dismiss(sender.TagOf<AttentionItem>());

    /// <summary>
    ///     Opens the logs of the container an item is about, or the cleanup dialog for reclaimable space.
    /// </summary>
    private Task RunPrimaryAsync(AttentionItem item)
    {
        if (item.Kind == AttentionKind.UnusedImages)
        {
            return CleanupAsync(UnusedImagesOnly);
        }

        App.Window.ShowContainer(item.ContainerId);
        return Task.CompletedTask;
    }

    private void OnSuggestedImageClick(object sender, RoutedEventArgs e) => FirstImageBox.Text = (string)((HyperlinkButton)sender).Content;

    private void OnFirstImageKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            PullFirstImage();
            e.Handled = true;
        }
    }

    private void OnFirstPullClick(object sender, RoutedEventArgs e) => PullFirstImage();

    /// <summary>
    ///     Hands the image to the Images page, which pulls it and shows the progress and the result.
    /// </summary>
    private void PullFirstImage()
    {
        ImagesViewModel images = ((ImagesPage)App.Window.ShowPage("images")).ViewModel;
        images.PullReference = FirstImageBox.Text;
        if (images.PullCommand.CanExecute(null))
        {
            images.PullCommand.Execute(null);
        }
    }
}
