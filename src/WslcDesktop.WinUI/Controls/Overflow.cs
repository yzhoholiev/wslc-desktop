using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WslcDesktop.Controls;

/// <summary>
///     How a text that may not fit its place behaves, the same in every list: one line, an ellipsis, and the whole text as a tooltip.
/// </summary>
public static class Overflow
{
    public static readonly DependencyProperty ShortensProperty = DependencyProperty.RegisterAttached(
        "Shortens", typeof(bool), typeof(Overflow), new PropertyMetadata(false, OnShortensChanged));

    public static readonly DependencyProperty FullTextProperty = DependencyProperty.RegisterAttached(
        "FullText", typeof(string), typeof(Overflow), new PropertyMetadata(null, OnFullTextChanged));

    public static bool GetShortens(TextBlock text) => (bool)text.GetValue(ShortensProperty);

    public static void SetShortens(TextBlock text, bool value) => text.SetValue(ShortensProperty, value);

    /// <summary>
    ///     What the shown text stands for, such as a whole ID, which the tooltip shows even when the short form fits.
    /// </summary>
    public static string? GetFullText(TextBlock text) => (string?)text.GetValue(FullTextProperty);

    public static void SetFullText(TextBlock text, string? value) => text.SetValue(FullTextProperty, value);

    private static void OnShortensChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock text || !(bool)e.NewValue)
        {
            return;
        }

        text.TextWrapping = TextWrapping.NoWrap;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.IsTextTrimmedChanged += (sender, _) => ShowHint(sender);
        text.RegisterPropertyChangedCallback(TextBlock.TextProperty, (sender, _) => ShowHint((TextBlock)sender));
        ShowHint(text);
    }

    private static void OnFullTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock text)
        {
            return;
        }

        if (GetShortens(text))
        {
            ShowHint(text);
        }
        else
        {
            SetShortens(text, true);
        }
    }

    private static void ShowHint(TextBlock text)
    {
        string? full = GetFullText(text);
        string? hint = !string.IsNullOrEmpty(full) && full != text.Text ? full : text.IsTextTrimmed ? text.Text : null;
        ToolTipService.SetToolTip(text, hint);
    }
}
