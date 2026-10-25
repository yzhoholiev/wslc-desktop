using WslcDesktop.Formatting;

namespace WslcDesktop.Tests.Formatting;

public sealed class JsonTokenizerTests
{
    [Theory]
    [InlineData("{\"a\":1}", true)]
    [InlineData("  \n[1, 2]", true)]
    [InlineData("Loading…", false)]
    [InlineData("", false)]
    [InlineData("Error: no such container", false)]
    public void LooksLikeJson_ChecksTheFirstCharacter(string text, bool expected) => Assert.Equal(expected, JsonTokenizer.LooksLikeJson(text));

    [Fact]
    public void Tokenize_ClassifiesKeysStringsNumbersAndLiterals()
    {
        var tokens = JsonTokenizer.Tokenize("""{"Name": "web", "Port": -80, "Ratio": 1.5e3, "On": true, "Off": false, "None": null}""")
            .Where(token => token.Kind != JsonTokenKind.Plain)
            .ToList();

        Assert.Equal(
            [
                new JsonToken("\"Name\"", JsonTokenKind.Key),
                new JsonToken("\"web\"", JsonTokenKind.String),
                new JsonToken("\"Port\"", JsonTokenKind.Key),
                new JsonToken("-80", JsonTokenKind.Number),
                new JsonToken("\"Ratio\"", JsonTokenKind.Key),
                new JsonToken("1.5e3", JsonTokenKind.Number),
                new JsonToken("\"On\"", JsonTokenKind.Key),
                new JsonToken("true", JsonTokenKind.Literal),
                new JsonToken("\"Off\"", JsonTokenKind.Key),
                new JsonToken("false", JsonTokenKind.Literal),
                new JsonToken("\"None\"", JsonTokenKind.Key),
                new JsonToken("null", JsonTokenKind.Literal)
            ],
            tokens);
    }

    [Fact]
    public void Tokenize_KeepsEscapedQuotesInsideStrings()
    {
        JsonToken token = JsonTokenizer.Tokenize("""["say \"hi\" now"]""").Single(token => token.Kind == JsonTokenKind.String);

        Assert.Equal("\"say \\\"hi\\\" now\"", token.Text);
    }

    [Fact]
    public void Tokenize_TreatsAKeyFollowedByWhitespaceAsAKey()
    {
        JsonToken token = JsonTokenizer.Tokenize("{\"Key\"   \n : 1}").First(token => token.Kind != JsonTokenKind.Plain);

        Assert.Equal(JsonTokenKind.Key, token.Kind);
    }

    [Fact]
    public void Tokenize_LeavesOtherWordsAndAnUnterminatedStringReadable()
    {
        Assert.Equal(JsonTokenKind.Plain, Assert.Single(JsonTokenizer.Tokenize("[undefined]"), token => token.Text.Contains("undefined")).Kind);
        Assert.Equal(JsonTokenKind.Plain, Assert.Single(JsonTokenizer.Tokenize("[nullable]"), token => token.Text.Contains("nullable")).Kind);
        Assert.Equal("\"open", JsonTokenizer.Tokenize("[\"open").Last().Text);
    }

    [Theory]
    [InlineData("""{"a": [1, true, "x"], "b": {"c": null}}""")]
    [InlineData("[\n  {\n    \"Id\": \"sha256:abc\",\n    \"Size\": 4400000\n  }\n]")]
    public void Tokenize_LosesNoText(string json) => Assert.Equal(json, string.Concat(JsonTokenizer.Tokenize(json).Select(token => token.Text)));

    [Theory]
    [InlineData("[-12.5e+3", "-12.5e+3", JsonTokenKind.Number)]
    [InlineData("[1, true", "true", JsonTokenKind.Literal)]
    [InlineData("{\"a\"", "\"a\"", JsonTokenKind.String)]
    public void Tokenize_ReadsATokenThatEndsTheText(string json, string text, JsonTokenKind kind) =>
        Assert.Equal(new JsonToken(text, kind), JsonTokenizer.Tokenize(json).Last());
}
