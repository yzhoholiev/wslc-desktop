using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace WslcDesktop.Controls;

/// <summary>
///     List row layout with the hand cursor, whose element named RowActions shows only on hover or keyboard focus.
/// </summary>
public sealed partial class ClickableRow : Grid
{
    private const string ActionsName = "RowActions";

    private UIElement? _actions;
    private bool _hasFocus;
    private bool _isHovered;

    public ClickableRow()
    {
        // ProtectedCursor is only settable from a subclass.
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);

        // Without a background, the gaps between cells are not hit-testable and would end the hover.
        Background = new SolidColorBrush(Colors.Transparent);

        Loaded += (_, _) =>
        {
            _actions = FindName(ActionsName) as UIElement;
            if (_actions is not null)
            {
                _actions.OpacityTransition = new ScalarTransition { Duration = TimeSpan.FromMilliseconds(120) };
                UpdateActions();
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

    private void UpdateActions()
    {
        if (_actions is not null)
        {
            _actions.Opacity = _isHovered || _hasFocus ? 1 : 0;
        }
    }
}
