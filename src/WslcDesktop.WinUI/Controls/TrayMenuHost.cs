using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;
using WslcDesktop.Interop;

namespace WslcDesktop.Controls;

/// <summary>
///     Shows the tray menu as a Fluent <see cref="MenuFlyout" />, anchored to a borderless 1×1 window placed at the cursor.
/// </summary>
internal sealed class TrayMenuHost
{
    private readonly Grid _anchor = new();
    private readonly nint _hwnd;
    private readonly Window _window;
    private MenuFlyout? _flyout;
    private bool _isClosed;

    public TrayMenuHost()
    {
        _window = new Window { Content = _anchor, Title = AppIdentity.DisplayName };
        _hwnd = WindowNative.GetWindowHandle(_window);

        _window.AppWindow.IsShownInSwitchers = false;
        PopupWindow.MakeInvisibleToolWindow(_hwnd);

        // Clicking anywhere outside the app deactivates the host, which must dismiss the menu like a native one.
        _window.Activated += (_, args) =>
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                _flyout?.Hide();
            }
        };
    }

    public void Show(TrayMenuItem[] items, PointInt32 cursor)
    {
        _flyout?.Hide();

        var flyout = new MenuFlyout { ShouldConstrainToRootBounds = false };
        foreach (TrayMenuItem item in items)
        {
            flyout.Items.Add(CreateItem(item));
        }

        flyout.Closed += (_, _) =>
        {
            if (_flyout == flyout)
            {
                _flyout = null;

                // A click on Exit closes the host before the flyout reports that it closed.
                if (!_isClosed)
                {
                    _window.AppWindow.Hide();
                }
            }
        };

        _flyout = flyout;

        // The cursor is over the taskbar; anchoring inside the work area opens the menu beside it on whichever edge it is docked.
        RectInt32 workArea = DisplayArea.GetFromPoint(cursor, DisplayAreaFallback.Nearest).WorkArea;
        int anchorX = Math.Clamp(cursor.X, workArea.X, workArea.X + workArea.Width - 1);
        int anchorY = Math.Clamp(cursor.Y, workArea.Y, workArea.Y + workArea.Height - 1);
        // AppWindow sizing would enforce the presenter's minimum window size, so position the 1×1 host directly.
        PopupWindow.MoveTopmost(_hwnd, anchorX, anchorY);
        _window.AppWindow.Show();
        _window.Activate();
        Foreground.Request(_hwnd);

        if (_anchor.XamlRoot is null)
        {
            void OnLoaded(object sender, RoutedEventArgs e)
            {
                _anchor.Loaded -= OnLoaded;
                Open(flyout);
            }

            _anchor.Loaded += OnLoaded;
        }
        else
        {
            Open(flyout);
        }
    }

    public void Close()
    {
        _isClosed = true;
        _window.Close();
    }

    private void Open(MenuFlyout flyout)
    {
        if (_flyout == flyout)
        {
            // Opens from the cursor upwards and to the left, where a notification-area menu is expected to appear.
            flyout.ShowAt(_anchor, new FlyoutShowOptions
            {
                Position = new Point(0, 0),
                Placement = FlyoutPlacementMode.TopEdgeAlignedRight
            });
        }
    }

    private static MenuFlyoutItemBase CreateItem(TrayMenuItem item)
    {
        if (item.IsSeparator)
        {
            return new MenuFlyoutSeparator();
        }

        IconElement? icon = CreateIcon(item);
        if (item.Children is { Length: > 0 } children)
        {
            var subItem = new MenuFlyoutSubItem { Text = item.Text, Icon = icon, IsEnabled = item.IsEnabled };
            foreach (TrayMenuItem child in children)
            {
                subItem.Items.Add(CreateItem(child));
            }

            return subItem;
        }

        // No toggle items: a single check mark makes WinUI add a check column to every item and shifts all icons right.
        var menuItem = new MenuFlyoutItem { Text = item.Text, Icon = icon, IsEnabled = item.IsEnabled };
        if (item.IsDefault)
        {
            menuItem.FontWeight = FontWeights.SemiBold;
        }

        if (item.OnClick is { } onClick)
        {
            menuItem.Click += (_, _) => onClick();
        }

        return menuItem;
    }

    private static IconElement? CreateIcon(TrayMenuItem item) => item switch
    {
        { DotColor: { } rgb } => new PathIcon
        {
            Data = new EllipseGeometry { Center = new Point(8, 8), RadiusX = 5, RadiusY = 5 },
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb))
        },
        { Glyph: { } glyph } => new FontIcon { Glyph = glyph },
        _ => null
    };
}
