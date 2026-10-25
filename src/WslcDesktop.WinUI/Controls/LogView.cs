using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WslcDesktop.Formatting;

namespace WslcDesktop.Controls;

/// <summary>
///     Selectable container log output with dimmed timestamps, colored warning and error lines, and ANSI colors.
/// </summary>
public sealed partial class LogView : MonospaceView
{
    /// <summary>
    ///     Thousands of runs make the text block slow to lay out; long tails stay readable as plain text.
    /// </summary>
    private const int MaxColoredLines = 5000;

    public static readonly DependencyProperty ShowTimestampsProperty = DependencyProperty.Register(
        nameof(ShowTimestamps), typeof(bool), typeof(LogView), new PropertyMetadata(true, OnRenderInputChanged));

    private static readonly Palette DarkPalette = new(
        0xF1707B,
        0xE3B341,
        [
            0x000000, 0xCD3131, 0x0DBC79, 0xE5E510, 0x2472C8, 0xBC3FBC, 0x11A8CD, 0xE5E5E5,
            0x666666, 0xF14C4C, 0x23D18B, 0xF5F543, 0x3B8EEA, 0xD670D6, 0x29B8DB, 0xFFFFFF
        ]);

    private static readonly Palette LightPalette = new(
        0xC42B1C,
        0x9D5D00,
        [
            0x000000, 0xCD3131, 0x00BC00, 0x949800, 0x0451A5, 0xBC05BC, 0x0598BC, 0x555555,
            0x666666, 0xCD3131, 0x14CE14, 0xB5BA00, 0x0451A5, 0xBC05BC, 0x0598BC, 0xA5A5A5
        ]);

    private readonly Dictionary<int, SolidColorBrush> _brushes = [];

    private Brush _dim = null!;

    private Palette _palette = DarkPalette;

    /// <summary>
    ///     Whether the timestamp that wslc puts at the start of each line is shown.
    /// </summary>
    public bool ShowTimestamps
    {
        get => (bool)GetValue(ShowTimestampsProperty);
        set => SetValue(ShowTimestampsProperty, value);
    }

    protected override void Render()
    {
        Output.Inlines.Clear();
        _palette = IsLightTheme ? LightPalette : DarkPalette;
        _dim = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];

        ReadOnlySpan<char> text = Text;
        if (IsHighContrast || text.Count('\n') >= MaxColoredLines)
        {
            Output.Inlines.Add(new Run { Text = LogParser.ToPlainText(Text, ShowTimestamps) });
            return;
        }

        var segments = new List<LogSegment>();
        foreach (Range line in text.Split('\n'))
        {
            if (line.Start.Value > 0)
            {
                Output.Inlines.Add(new LineBreak());
            }

            segments.Clear();
            LogParser.ParseLine(text[line].TrimEnd('\r'), ShowTimestamps, segments);
            foreach (LogSegment segment in segments)
            {
                Output.Inlines.Add(ToRun(segment));
            }
        }
    }

    private Run ToRun(LogSegment segment)
    {
        var run = new Run { Text = segment.Text };
        if (BrushFor(segment) is { } brush)
        {
            run.Foreground = brush;
        }

        if (segment.IsBold)
        {
            run.FontWeight = FontWeights.Bold;
        }

        return run;
    }

    private Brush? BrushFor(LogSegment segment) => segment switch
    {
        { Rgb: { } rgb } => Solid(rgb),
        { Ansi: { } ansi } => Solid(_palette.Ansi[ansi]),
        { Tone: LogTone.Error } => Solid(_palette.Error),
        { Tone: LogTone.Warning } => Solid(_palette.Warning),
        { Tone: LogTone.Timestamp or LogTone.Dim } => _dim,
        _ => null
    };

    private SolidColorBrush Solid(int rgb)
    {
        if (_brushes.TryGetValue(rgb, out SolidColorBrush? brush))
        {
            return brush;
        }

        brush = new SolidColorBrush(Color.FromArgb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
        _brushes[rgb] = brush;

        return brush;
    }

    private sealed record Palette(int Error, int Warning, int[] Ansi);
}
