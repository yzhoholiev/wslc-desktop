using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WslcDesktop.Services;
using WslcDesktop.Views;

namespace WslcDesktop;

public sealed partial class MainWindow
{
    private const string SettingsTag = "settings";

    private readonly Dictionary<string, Page> _pages = [];
    private string _currentTag = "dashboard";

    private Dictionary<string, Func<Page>> PageFactories => field ??= new Dictionary<string, Func<Page>>
    {
        ["dashboard"] = () => new DashboardPage(Services),
        ["containers"] = () => new ContainersPage(Services),
        ["images"] = () => new ImagesPage(Services),
        ["volumes"] = () => new VolumesPage(Services),
        ["networks"] = () => new NetworksPage(Services),
        ["wslc"] = () => new WslcPage(Services),
        ["wsl"] = () => new WslPage(Services),
        [SettingsTag] = () => new SettingsPage(Services)
    };

    /// <summary>
    ///     Shows the window on the page with the given navigation tag and returns that page.
    /// </summary>
    public Page ShowPage(string tag)
    {
        // Set first, so a window shown from the tray builds this page rather than the one it was hidden on.
        _currentTag = tag;
        ShowAndActivate();
        Nav.SelectedItem = tag == SettingsTag
            ? Nav.SettingsItem
            : Nav.MenuItems.Concat(Nav.FooterMenuItems).OfType<NavigationViewItem>().First(item => (string)item.Tag == tag);

        return GetOrCreatePage(tag);
    }

    /// <summary>
    ///     Gives the pages Ctrl+1, Ctrl+2 and so on, in the order the navigation pane lists them.
    /// </summary>
    private void AddPageShortcuts()
    {
        Nav.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
        int number = 0;
        foreach (NavigationViewItem item in Nav.MenuItems.Concat(Nav.FooterMenuItems).OfType<NavigationViewItem>())
        {
            string tag = (string)item.Tag;
            var shortcut = new KeyboardAccelerator { Modifiers = VirtualKeyModifiers.Control, Key = VirtualKey.Number1 + number++ };
            shortcut.Invoked += (_, args) =>
            {
                ShowPage(tag);
                args.Handled = true;
            };

            Nav.KeyboardAccelerators.Add(shortcut);
        }
    }

    private DashboardPage ShowDashboard() => (DashboardPage)ShowPage("dashboard");

    public void ShowContainer(string id) => ((ContainersPage)ShowPage("containers")).ShowContainer(id);

    /// <summary>
    ///     Opens the Images page with the details of the image a container was created from.
    /// </summary>
    public void ShowImageOf(string containerImage, string containerImageId) =>
        ((ImagesPage)ShowPage("images")).ViewModel.SelectWhenAvailable(image =>
            ResourceUsage.IsImage(containerImage, containerImageId, image.Id, image.Reference));

    public void ShowVolume(string name) =>
        ((VolumesPage)ShowPage("volumes")).ViewModel.SelectWhenAvailable(volume => ResourceUsage.VolumeNameMatches(name, volume.Name));

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        string? tag = args.IsSettingsSelected ? SettingsTag : args.SelectedItemContainer?.Tag as string;
        if (tag is not null && PageFactories.ContainsKey(tag))
        {
            _currentTag = tag;
            PageHost.Child = GetOrCreatePage(tag);
        }
    }

    private Page GetOrCreatePage(string tag)
    {
        if (!_pages.TryGetValue(tag, out Page? page))
        {
            page = PageFactories[tag]();
            _pages[tag] = page;
        }

        return page;
    }

    private void OnPaneToggleRequested(TitleBar sender, object args)
    {
        Nav.IsPaneOpen = !Nav.IsPaneOpen;
        Services.Settings.Current.IsNavigationPaneOpen = Nav.IsPaneOpen;
        Services.Settings.Save();
    }
}
