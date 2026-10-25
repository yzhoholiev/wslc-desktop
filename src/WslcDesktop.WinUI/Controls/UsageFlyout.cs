using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WslcDesktop.Models;
using WslcDesktop.ViewModels;

namespace WslcDesktop.Controls;

/// <summary>
///     Menu of the containers that use a resource, each opening that container's details.
/// </summary>
public static class UsageFlyout
{
    public static void ShowAt(FrameworkElement target, ContainerDto[] containers)
    {
        var flyout = new MenuFlyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
        if (containers.Length == 0)
        {
            flyout.Items.Add(new MenuFlyoutItem { Text = "No container found", IsEnabled = false });
        }

        foreach (ContainerDto container in containers)
        {
            var item = new MenuFlyoutItem
            {
                Text = container.Names,
                KeyboardAcceleratorTextOverride = Labels.StateLabel(container.State),
                Icon = new FontIcon { Glyph = Glyphs.Dot, FontSize = 8, Foreground = Bindings.StateBrush(container.State) }
            };

            ToolTipService.SetToolTip(item, $"Open {container.Names}");
            item.Click += (_, _) => App.Window.ShowContainer(container.Id);
            flyout.Items.Add(item);
        }

        flyout.ShowAt(target);
    }
}
