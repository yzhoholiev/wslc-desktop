using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Views;

public sealed partial class SettingsPage
{
    public SettingsPage(AppServices services)
    {
        ViewModel = new SettingsViewModel(services);
        InitializeComponent();
        BackdropChoice.SelectedItem = BackdropChoice.Items.OfType<RadioButton>()
            .FirstOrDefault(option => (string)option.Tag == ViewModel.Backdrop) ?? BackdropChoice.Items[0];
    }

    public SettingsViewModel ViewModel { get; }

    private void OnBackdropChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BackdropChoice.SelectedItem is RadioButton { Tag: string backdrop } && ViewModel.ChangeBackdrop(backdrop))
        {
            App.Window.ApplyBackdrop(backdrop);
        }
    }

    private void OnStartWithWindowsToggled(object sender, RoutedEventArgs e) =>
        StartWithWindowsSwitch.IsOn = ViewModel.SetStartWithWindows(StartWithWindowsSwitch.IsOn);

    private void OnOpenDataFolderClick(object sender, RoutedEventArgs e) => ViewModel.OpenDataFolder();

    private void OnOpenLicensesClick(object sender, RoutedEventArgs e) => ViewModel.OpenLicenses();
}
