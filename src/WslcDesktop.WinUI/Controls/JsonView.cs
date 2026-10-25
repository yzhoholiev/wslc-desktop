using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WslcDesktop.Formatting;

namespace WslcDesktop.Controls;

/// <summary>
///     Selectable monospace text that colors keys, strings, numbers and literals when the text is JSON.
/// </summary>
public sealed partial class JsonView : MonospaceView
{
    private static readonly Palette DarkPalette = new(
        Rgb(0x9C, 0xDC, 0xFE), Rgb(0xCE, 0x91, 0x78), Rgb(0xB5, 0xCE, 0xA8), Rgb(0x56, 0x9C, 0xD6));

    private static readonly Palette LightPalette = new(
        Rgb(0x04, 0x51, 0xA5), Rgb(0xA3, 0x15, 0x15), Rgb(0x09, 0x86, 0x58), Rgb(0x00, 0x00, 0xFF));

    private static SolidColorBrush Rgb(byte r, byte g, byte b) => new(Color.FromArgb(0xFF, r, g, b));

    protected override void Render()
    {
        Output.Inlines.Clear();
        string text = Text;
        if (IsHighContrast || !JsonTokenizer.LooksLikeJson(text))
        {
            Output.Inlines.Add(new Run { Text = text });
            return;
        }

        Palette palette = IsLightTheme ? LightPalette : DarkPalette;
        foreach (JsonToken token in JsonTokenizer.Tokenize(text))
        {
            var run = new Run { Text = token.Text };
            if (palette.For(token.Kind) is { } brush)
            {
                run.Foreground = brush;
            }

            Output.Inlines.Add(run);
        }
    }

    private sealed record Palette(Brush Key, Brush String, Brush Number, Brush Literal)
    {
        public Brush? For(JsonTokenKind kind) => kind switch
        {
            JsonTokenKind.Key => Key,
            JsonTokenKind.String => String,
            JsonTokenKind.Number => Number,
            JsonTokenKind.Literal => Literal,
            _ => null
        };
    }
}
