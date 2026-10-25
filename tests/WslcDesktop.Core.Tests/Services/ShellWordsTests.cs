using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class ShellWordsTests
{
    [Theory]
    [InlineData("", new string[0])]
    [InlineData("   ", new string[0])]
    [InlineData("sh -c 'echo hi'", new[] { "sh", "-c", "echo hi" })]
    [InlineData("nginx -g \"daemon off;\"", new[] { "nginx", "-g", "daemon off;" })]
    [InlineData("echo \"say \\\"hi\\\"\"", new[] { "echo", "say \"hi\"" })]
    [InlineData("a\\ b c", new[] { "a b", "c" })]
    [InlineData("pre'quoted part'post", new[] { "prequoted partpost" })]
    [InlineData("''", new[] { "" })]
    [InlineData("  spaced   out  ", new[] { "spaced", "out" })]
    public void Split_ReadsWordsTheWayAShellQuotesThem(string text, string[] expected) => Assert.Equal(expected, ShellWords.Split(text));

    [Theory]
    [InlineData("echo 'unclosed")]
    [InlineData("echo \"unclosed")]
    public void Split_RejectsAnUnclosedQuote(string text) => Assert.Throws<FormatException>(() => ShellWords.Split(text));

    [Fact]
    public void Join_QuotesOnlyWordsThatNeedIt()
    {
        Assert.Equal("nginx -g 'daemon off;'", ShellWords.Join(["nginx", "-g", "daemon off;"]));
        Assert.Equal("/docker-entrypoint.sh", ShellWords.Join(["/docker-entrypoint.sh"]));
        Assert.Equal("''", ShellWords.Join([""]));
    }

    [Fact]
    public void Join_RoundTripsThroughSplit()
    {
        string[] quotes = ["sh", "-c", "echo 'it''s' && ls *"];
        string[] specials = ["a\"b", "back\\slash", "tab\there", ""];

        Assert.Equal(quotes, ShellWords.Split(ShellWords.Join(quotes)));
        Assert.Equal(specials, ShellWords.Split(ShellWords.Join(specials)));
    }
}
