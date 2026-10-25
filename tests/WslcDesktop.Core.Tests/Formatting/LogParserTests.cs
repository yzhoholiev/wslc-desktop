using WslcDesktop.Formatting;

namespace WslcDesktop.Tests.Formatting;

public sealed class LogParserTests
{
    private const string Stamp = "2026-10-19T14:06:19.832711472Z ";

    private static readonly Dictionary<string, (string Line, bool ShowTimestamps, LogSegment[] Expected)> Lines = new()
    {
        ["timestamp, shown dimmed"] = (Stamp + "server started", true, [new LogSegment(Stamp, LogTone.Timestamp), new LogSegment("server started")]),
        ["timestamp, hidden"] = (Stamp + "server started", false, [new LogSegment("server started")]),
        ["text that only looks like a timestamp"] =
            ("2026/10/19 13:40:24 [notice] start", true, [new LogSegment("2026/10/19 13:40:24 [notice] start")]),
        ["basic and bright colors, and resets"] = (
            "\e[32mINFO\e[0m ok \e[91mhot\e[39m", true,
            [new LogSegment("INFO", Ansi: 2), new LogSegment(" ok "), new LogSegment("hot", Ansi: 9)]),
        ["bold with a color"] = ("\e[1;33mWARN\e[0m slow", true, [new LogSegment("WARN", Ansi: 3, IsBold: true), new LogSegment(" slow")]),
        ["dim text without a color"] = ("\e[2mquiet\e[22m loud", true, [new LogSegment("quiet", LogTone.Dim), new LogSegment(" loud")]),
        ["other escape sequences stripped"] = ("progress\e[K 50%", true, [new LogSegment("progress"), new LogSegment(" 50%")]),
        ["empty SGR as a reset"] = ("\e[31ma\e[mb", true, [new LogSegment("a", Ansi: 1), new LogSegment("b")]),
        ["empty code as a reset"] = ("\e[1;;31mx", true, [new LogSegment("x", Ansi: 1)]),
        ["code too large as a reset"] = ("\e[1;99999999999mx", true, [new LogSegment("x")]),
        ["long parameter list"] = ("\e[" + string.Concat(Enumerable.Repeat("1;", 40)) + "31mx", true, [new LogSegment("x", Ansi: 1, IsBold: true)]),
        ["escape right after a shown timestamp"] = (Stamp + "\e[31mred", true, [new LogSegment(Stamp, LogTone.Timestamp), new LogSegment("red", Ansi: 1)]),
        ["escape right after a hidden timestamp"] = (Stamp + "\e[31mred", false, [new LogSegment("red", Ansi: 1)]),
        ["unknown escape kept as text"] = ("a\e]0;title\ab", true, [new LogSegment("a\e]0;title\ab")]),
        ["escape cut off at the end kept as text"] = ("a\e[31", true, [new LogSegment("a\e[31")]),
        ["level word in a colored line not toned"] = ("\e[0mERROR x", true, [new LogSegment("ERROR x")]),
        ["carriage return after the timestamp, as mssql writes"] = (
            Stamp + "\r2026-10-24 09:10:43.12 spid52 Starting up", true,
            [new LogSegment(Stamp, LogTone.Timestamp), new LogSegment("2026-10-24 09:10:43.12 spid52 Starting up")]),
        ["progress redrawn with carriage returns"] = ("10%\r20%\r30% error", true, [new LogSegment("30% error", LogTone.Error)]),
        ["trailing carriage returns"] = ("done\r\r", true, [new LogSegment("done")]),
        ["color set before a carriage return"] = ("\e[32m10%\r30%", true, [new LogSegment("30%", Ansi: 2)]),
        ["empty line"] = ("", true, [])
    };

    private static readonly Dictionary<string, (string Log, bool ShowTimestamps, string Expected)> PlainTexts = new()
    {
        ["timestamps kept, escapes stripped"] = (Stamp + "\e[32mok\e[0m\r\n\nno stamp\e[K", true, Stamp + "ok\n\nno stamp"),
        ["timestamp of every line stripped"] = (Stamp + "\e[32mok\e[0m\r\n\nno stamp\n" + Stamp + "next", false, "ok\n\nno stamp\nnext"),
        ["what follows the last carriage return of a line kept"] =
            (Stamp + "\rstarted\r\n" + Stamp + "10%\r30%", true, Stamp + "started\n" + Stamp + "30%")
    };

    public static TheoryData<string> LineNames { get; } = new(Lines.Keys);

    public static TheoryData<string> PlainTextNames { get; } = new(PlainTexts.Keys);

    [Theory]
    [MemberData(nameof(LineNames))]
    public void ParseLine_RendersTheLine(string caseName)
    {
        (string? line, bool showTimestamps, LogSegment[]? expected) = Lines[caseName];

        Assert.Equal(expected, Parse(line, showTimestamps));
    }

    [Theory]
    [InlineData("2026-10-19T14:06:19Z ")]
    [InlineData("2026-10-19T14:06:19.5+03:00 ")]
    public void ParseLine_RecognizesTimestampVariants(string stamp) =>
        Assert.Equal(LogTone.Timestamp, Parse(stamp + "x", true).First().Tone);

    [Theory]
    [InlineData("ERROR: connection refused", LogTone.Error)]
    [InlineData("2026/10/19 [error] 1#1: open() failed", LogTone.Error)]
    [InlineData("panic: runtime error", LogTone.Error)]
    [InlineData("Unhandled exception. System.Exception", LogTone.Error)]
    [InlineData("WARNING:  could not flush", LogTone.Warning)]
    [InlineData("level=warn msg=slow", LogTone.Warning)]
    [InlineData("LOG:  checkpoint complete", LogTone.Default)]
    [InlineData("writing to stderr", LogTone.Default)]
    [InlineData("errors=0 warnings=0", LogTone.Default)]
    public void ParseLine_ColorsLinesByLevelWord(string line, LogTone expected) =>
        Assert.Equal(expected, Assert.Single(Parse(line, true)).Tone);

    [Theory]
    [InlineData("\e[38;5;9mx", 9, null)]
    [InlineData("\e[38;5;208mx", null, 0xFF8700)]
    [InlineData("\e[38;5;16mx", null, 0x000000)]
    [InlineData("\e[38;5;231mx", null, 0xFFFFFF)]
    [InlineData("\e[38;5;232mx", null, 0x080808)]
    [InlineData("\e[38;5;255mx", null, 0xEEEEEE)]
    [InlineData("\e[38;2;120;180;255mx", null, 0x78B4FF)]
    public void ParseLine_RendersExtendedColors(string line, int? ansi, int? rgb)
    {
        LogSegment segment = Assert.Single(Parse(line, true));

        Assert.Equal(ansi, segment.Ansi);
        Assert.Equal(rgb, segment.Rgb);
    }

    [Fact]
    public void ParseLine_IgnoresBackgroundsIncludingTheirArguments()
    {
        Assert.Equal(new LogSegment("x", Ansi: 1), Assert.Single(Parse("\e[48;5;196;31mx", true)));
        Assert.Equal(new LogSegment("x", Ansi: 4), Assert.Single(Parse("\e[48;2;1;2;3;34mx", true)));
        Assert.Equal(new LogSegment("x"), Assert.Single(Parse("\e[41mx", true)));
    }

    [Theory]
    [MemberData(nameof(PlainTextNames))]
    public void ToPlainText_CopiesTheLogAsText(string caseName)
    {
        (string log, bool showTimestamps, string expected) = PlainTexts[caseName];

        Assert.Equal(expected, LogParser.ToPlainText(log, showTimestamps));
    }

    private static List<LogSegment> Parse(string line, bool showTimestamp)
    {
        var segments = new List<LogSegment>();
        LogParser.ParseLine(line, showTimestamp, segments);
        return segments;
    }
}
