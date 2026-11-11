using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.ComponentModel;
using Windows.System;
using WslcDesktop.Controls;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Views;

public sealed partial class ContainersPage
{
    private readonly AppServices _services;
    private bool _isRestoringHighlight;

    public ContainersPage(AppServices services)
    {
        _services = services;
        ViewModel = new ContainersViewModel(services);
        InitializeComponent();
        this.FollowMonitor(services.Monitor, OnRuntimeUpdated);
        Loaded += (_, _) => OnRuntimeUpdated();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ContainerList.SizeChanged += OnListSizeChanged;
        ViewModel.Items.CollectionChanged += (_, _) => RestoreHighlight();
        KeyDown += OnKeyDown;
    }

    public ContainersViewModel ViewModel { get; }

    /// <summary>
    ///     Opens the details pane for a container, as soon as it appears in the list.
    /// </summary>
    public void ShowContainer(string id)
    {
        ViewModel.SelectWhenAvailable(id);
        OnRuntimeUpdated();
    }

    private void OnRuntimeUpdated()
    {
        ViewModel.Apply(_services.Monitor.Latest);
        if (ContainerList.SelectedItem != ViewModel.Selected)
        {
            ContainerList.SelectedItem = ViewModel.Selected;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ContainersViewModel.Selected))
        {
            return;
        }

        ContainerList.SelectedItem = ViewModel.Selected;
        if (ViewModel.Selected is { } selected)
        {
            ContainerList.ScrollIntoView(selected);
        }
    }

    /// <summary>
    ///     Keeps the selected row in view when the list is first laid out or gets shorter, as it does when the details pane opens below it.
    /// </summary>
    private void OnListSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if ((e.PreviousSize.Height == 0 || e.NewSize.Height < e.PreviousSize.Height) && ViewModel.Selected is { } selected)
        {
            ContainerList.ScrollIntoView(selected);
        }
    }

    /// <summary>
    ///     Restores the row highlight, which the ListView drops when a sort or search moves the selected item.
    /// </summary>
    private void RestoreHighlight()
    {
        // Deferred until the ListView has applied the change itself, and once for a whole reordering.
        if (_isRestoringHighlight)
        {
            return;
        }

        _isRestoringHighlight = DispatcherQueue.TryEnqueue(() =>
        {
            _isRestoringHighlight = false;
            if (ViewModel.Selected is { } selected && ContainerList.SelectedItem != selected)
            {
                ContainerList.SelectedItem = selected;
            }
        });
    }

    private void OnFindInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SearchBox.Focus(FocusState.Keyboard);
        SearchBox.SelectAll();
        args.Handled = true;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && ViewModel.IsDetailsOpen)
        {
            ViewModel.CloseDetails();
            e.Handled = true;
        }
    }

    private async void OnListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Delete && ViewModel.Selected is { } selected)
        {
            e.Handled = true;
            await RemoveAsync(selected);
        }
    }

    private void OnLogsSizeChanged(object sender, SizeChangedEventArgs e) =>
        LogsScroller.ChangeView(null, LogsScroller.ScrollableHeight, null, true);

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Ignore deselection caused by list reordering; closing the details pane clears the selection explicitly.
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ContainerItem item && item != ViewModel.Selected)
        {
            ViewModel.Selected = item;
        }
    }

    private async void OnStartStopClick(object sender, RoutedEventArgs e) => await ViewModel.StartOrStopAsync(sender.TagOf<ContainerItem>());

    private async void OnRestartClick(object sender, RoutedEventArgs e) => await ViewModel.RestartAsync(sender.TagOf<ContainerItem>());

    private void OnShellClick(object sender, RoutedEventArgs e) => _services.Shell.OpenContainerShell(sender.TagOf<ContainerItem>().Id);

    private void OnImageClick(object sender, RoutedEventArgs e)
    {
        ContainerItem item = sender.TagOf<ContainerItem>();
        App.Window.ShowImageOf(item.Image, item.ImageId);
    }

    private void OnDetailsImageClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is { } item)
        {
            App.Window.ShowImageOf(item.Image, item.ImageId);
        }
    }

    private void OnVolumeLinkClick(object? sender, string name) => App.Window.ShowVolume(name);

    private void OnCopyClick(object sender, RoutedEventArgs e) => ClipboardText.Copy(sender.TagOf<string>(), _services.Notifier);

    private void OnCopyInspectClick(object sender, RoutedEventArgs e) => ClipboardText.Copy(ViewModel.InspectText, _services.Notifier);

    private async void OnRemoveClick(object sender, RoutedEventArgs e) => await RemoveAsync(sender.TagOf<ContainerItem>());

    private async Task RemoveAsync(ContainerItem item)
    {
        string message = item.IsRunning
            ? $"'{item.Name}' is running. It will be stopped and deleted."
            : $"'{item.Name}' will be deleted.";

        if (await Dialogs.ConfirmAsync(XamlRoot, "Delete container?", message, "Delete"))
        {
            await ViewModel.RemoveAsync(item);
        }
    }

    private async void OnStopAllClick(object sender, RoutedEventArgs e)
    {
        if (await Dialogs.ConfirmStopAllContainersAsync(XamlRoot, _services.Monitor.Latest.RunningContainers))
        {
            await ViewModel.StopAllContainersAsync();
        }
    }

    private async void OnEmptyActionClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Empty.Reason == EmptyReason.NoItems)
        {
            App.Window.ShowPage("images");
            return;
        }

        await ViewModel.RunEmptyActionAsync();
    }

    private async void OnPruneClick(object sender, RoutedEventArgs e)
    {
        if (await Dialogs.ConfirmAsync(XamlRoot, "Remove stopped containers?", "All stopped containers will be deleted.", "Remove"))
        {
            await ViewModel.PruneAsync();
        }
    }
}
