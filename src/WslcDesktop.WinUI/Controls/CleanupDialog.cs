using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop.Controls;

public static class CleanupDialog
{
    /// <summary>
    ///     Lets the user choose which unused resources to remove.
    /// </summary>
    public static async Task<CleanupOptions?> ChooseAsync(XamlRoot xamlRoot, CleanupSummary summary, CleanupOptions? previous)
    {
        var stopRunning = new CheckBox
        {
            Content = summary.RunningContainers == 1
                ? "Stop the running container first, so it is removed too"
                : $"Stop the {summary.RunningContainers} running containers first, so they are removed too",
            IsChecked = summary.RunningContainers > 0 && previous?.StopRunningContainers == true,
            Visibility = summary.RunningContainers > 0 ? Visibility.Visible : Visibility.Collapsed
        };

        var containers = new CheckBox
        {
            Content = $"Stopped containers ({summary.StoppedContainers})",
            IsChecked = previous?.StoppedContainers ?? summary.StoppedContainers > 0
        };

        var networks = new CheckBox { Content = "Unused networks", IsChecked = previous?.UnusedNetworks ?? true };
        var images = new CheckBox
        {
            Content = $"Unused images ({Humanize.Count(summary.Images, "image")}, {summary.ImagesSize} in total)",
            IsChecked = previous?.UnusedImages ?? true
        };

        var taggedImages = new CheckBox
        {
            Content = "Include tagged images, not only dangling ones",
            IsChecked = previous?.IncludeTaggedImages ?? true,
            Margin = new Thickness(28, 0, 0, 0)
        };

        var volumes = new CheckBox
        {
            Content = $"Unused volumes ({Humanize.Count(summary.Volumes, "volume")} in total)",
            IsChecked = previous?.UnusedVolumes ?? true
        };

        var namedVolumes = new CheckBox
        {
            Content = "Include named volumes, not only anonymous ones",
            IsChecked = previous?.IncludeNamedVolumes ?? false,
            Margin = new Thickness(28, 0, 0, 0)
        };

        var namedVolumesWarning = new TextBlock
        {
            Text = "Named volumes usually hold database and application data. It cannot be recovered.",
            Margin = new Thickness(56, -4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"]
        };

        var intro = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };

        var content = new StackPanel
        {
            Spacing = 4,
            MinWidth = 440,
            Children = { intro, stopRunning, containers, networks, images, taggedImages, volumes, namedVolumes, namedVolumesWarning }
        };

        ContentDialog dialog = Dialogs.Create(xamlRoot, "Clean up unused resources", content, "Clean up");

        foreach (CheckBox box in new[] { stopRunning, containers, networks, images, taggedImages, volumes, namedVolumes })
        {
            box.Checked += Sync;
            box.Unchecked += Sync;
        }

        Sync(dialog, new RoutedEventArgs());

        if (await Dialogs.ShowAsync(dialog) != ContentDialogResult.Primary)
        {
            return null;
        }

        return new CleanupOptions(
            stopRunning.IsChecked == true,
            containers.IsChecked == true,
            networks.IsChecked == true,
            images.IsChecked == true,
            taggedImages.IsChecked == true,
            volumes.IsChecked == true,
            namedVolumes.IsChecked == true);

        void Sync(object sender, RoutedEventArgs e)
        {
            bool stopsRunning = stopRunning.IsChecked == true;
            intro.Text = stopsRunning
                ? "Choose what to remove. Running containers are stopped and removed along with everything else selected."
                : "Choose what to remove. Running containers and everything they use are kept.";

            // Stopping without removing would leave the containers holding their images, networks and volumes.
            if (stopsRunning)
            {
                containers.IsChecked = true;
            }

            containers.IsEnabled = !stopsRunning;
            taggedImages.IsEnabled = images.IsChecked == true;
            namedVolumes.IsEnabled = volumes.IsChecked == true;
            namedVolumesWarning.Visibility = namedVolumes is { IsEnabled: true, IsChecked: true } ? Visibility.Visible : Visibility.Collapsed;
            dialog.IsPrimaryButtonEnabled = new[] { containers, networks, images, volumes }.Any(box => box.IsChecked == true);
        }
    }
}
