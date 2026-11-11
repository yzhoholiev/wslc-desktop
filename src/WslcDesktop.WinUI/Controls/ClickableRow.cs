using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace WslcDesktop.Controls;

/// <summary>
///     List row layout with the hand cursor, whose element named RowActions shows only on hover, keyboard focus or selection,
///     and whose button named RowMenu opens the row's context menu.
/// </summary>
public sealed partial class ClickableRow : Grid
{
    private const string ActionsName = "RowActions";
    private const string MenuButtonName = "RowMenu";

    private UIElement? _actions;
    private SelectorItem? _container;
    private bool _hasFocus;
    private bool _isHovered;
    private Button? _menuButton;
    private long _selectionCallback;

    public ClickableRow()
    {
        // ProtectedCursor is only settable from a subclass.
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);

        // Without a background, the gaps between cells are not hit-testable and would end the hover.
        Background = new SolidColorBrush(Colors.Transparent);

        Loaded += (_, _) =>
        {
            _actions = FindName(ActionsName) as UIElement;
            _actions?.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(120) };

            // Loaded is raised again when the row re-enters the tree, which must not subscribe twice.
            if (_menuButton is null && FindName(MenuButtonName) is Button menuButton)
            {
                _menuButton = menuButton;
                menuButton.Click += (_, _) => ContextFlyout?.ShowAt(menuButton, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
            }

            WatchSelection(FindContainer());

            // The keyboard asks the focused list item for its menu, not the row inside it.
            _container?.ContextFlyout = ContextFlyout;
            UpdateActions();
        };

        // WinUI can raise Unloaded after the row was loaded again.
        Unloaded += (_, _) =>
        {
            if (!IsLoaded)
            {
                WatchSelection(null);
            }
        };

        PointerEntered += (_, _) => SetHovered(true);
        PointerExited += (_, _) => SetHovered(false);
        PointerCanceled += (_, _) => SetHovered(false);
        GotFocus += (_, _) => SetFocus(true);
        LostFocus += (_, _) => SetFocus(false);
    }

    private void SetHovered(bool value)
    {
        _isHovered = value;
        UpdateActions();
    }

    private void SetFocus(bool value)
    {
        _hasFocus = value;
        UpdateActions();
    }

    private SelectorItem? FindContainer()
    {
        DependencyObject? ancestor = VisualTreeHelper.GetParent(this);
        while (ancestor is not null and not SelectorItem)
        {
            ancestor = VisualTreeHelper.GetParent(ancestor);
        }

        return ancestor as SelectorItem;
    }

    private void WatchSelection(SelectorItem? container)
    {
        _container?.UnregisterPropertyChangedCallback(SelectorItem.IsSelectedProperty, _selectionCallback);
        _container = container;
        if (container is not null)
        {
            _selectionCallback = container.RegisterPropertyChangedCallback(SelectorItem.IsSelectedProperty, (_, _) => UpdateActions());
        }
    }

    private void UpdateActions()
    {
        if (_actions is null)
        {
            return;
        }

        bool shown = _isHovered || _hasFocus || _container?.IsSelected == true;
        _actions.Opacity = shown ? 1 : 0;

        // Invisible buttons would otherwise still take a tap meant for the row.
        _actions.IsHitTestVisible = shown;
    }
}
