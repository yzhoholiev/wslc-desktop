using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace WslcDesktop.Controls;

/// <summary>
///     A button that opens something elsewhere and shows the hand cursor a link has.
/// </summary>
public sealed partial class LinkButton : Button
{
    public LinkButton()
    {
        // ProtectedCursor is only settable from a subclass.
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }
}
