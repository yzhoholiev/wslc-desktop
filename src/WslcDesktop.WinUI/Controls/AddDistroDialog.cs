using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WslcDesktop.Services;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Controls;

public static class AddDistroDialog
{
    /// <summary>
    ///     A disabled ListViewItem does not dim content it was given as an element.
    /// </summary>
    private const double DisabledOpacity = 0.5;

    /// <summary>
    ///     What the dialog's title, hint, buttons and margins take of the window's height, leaving the rest to the list.
    /// </summary>
    private const double SurroundingHeight = 320;

    private const double MinListHeight = 120;
    private const double MaxListHeight = 360;

    /// <summary>
    ///     Lets the user pick one of the distributions WSL offers to install.
    /// </summary>
    public static async Task<AvailableDistroItem?> ChooseAsync(XamlRoot xamlRoot, Func<CancellationToken, Task<AvailableDistroItem[]>> listAvailable)
    {
        var loading = new ProgressRing { IsActive = true, Margin = new Thickness(0, 24, 0, 24) };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };

        // ContentDialog measures its content at full height and clips it, so the list scrolls within what the window leaves.
        var list = new ListView { SelectionMode = ListViewSelectionMode.Single, Visibility = Visibility.Collapsed };
        TextBlock hint = FormParts.Hint("The distribution installs in a terminal, where its first-time setup runs.");

        var content = new StackPanel { Spacing = 8, MinWidth = 440, Children = { hint, loading, status, list } };
        ContentDialog dialog = Dialogs.Create(xamlRoot, "Add a distribution", content, "Install");
        dialog.IsPrimaryButtonEnabled = false;
        list.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = Selected() is not null;

        FitList(xamlRoot, null);
        xamlRoot.Changed += FitList;
        using var closed = new CancellationTokenSource();
        _ = LoadAsync();

        try
        {
            return await Dialogs.ShowAsync(dialog) == ContentDialogResult.Primary ? Selected() : null;
        }
        finally
        {
            xamlRoot.Changed -= FitList;
            await closed.CancelAsync();
        }

        AvailableDistroItem? Selected() => (list.SelectedItem as ListViewItem)?.Tag as AvailableDistroItem;

        void FitList(XamlRoot root, XamlRootChangedEventArgs? e) =>
            list.MaxHeight = Math.Clamp(root.Size.Height - SurroundingHeight, MinListHeight, MaxListHeight);

        async Task LoadAsync()
        {
            string? message;
            try
            {
                AvailableDistroItem[] available = await listAvailable(closed.Token);
                foreach (AvailableDistroItem distro in available)
                {
                    list.Items.Add(CreateRow(distro));
                }

                message = available.Length == 0 ? "WSL offers no distributions to install." : null;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (CliException ex)
            {
                message = $"Could not get the list of distributions. {ex.Message}";
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
                message = "Could not show the list of distributions.";
            }

            loading.Visibility = Visibility.Collapsed;
            status.Text = message ?? "";
            status.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
            list.Visibility = message is null ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static ListViewItem CreateRow(AvailableDistroItem distro)
    {
        var name = new TextBlock { Text = distro.FriendlyName };
        Overflow.SetShortens(name, true);

        var detail = new TextBlock
        {
            Text = distro.IsInstalled ? "Installed" : distro.Name,
            Style = (Style)Application.Current.Resources["SecondaryTextStyle"]
        };

        Grid.SetColumn(detail, 1);

        var row = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Children = { name, detail },
            Opacity = distro.IsInstalled ? DisabledOpacity : 1
        };

        return new ListViewItem
        {
            Content = row,
            Tag = distro,
            IsEnabled = !distro.IsInstalled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
    }
}
