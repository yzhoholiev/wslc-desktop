using System.Buffers;

namespace WslcDesktop.Formatting;

public enum JsonTokenKind
{
    Plain,
    Key,
    String,
    Number,
    Literal
}

public readonly record struct JsonToken(string Text, JsonTokenKind Kind);

/// <summary>
///     Splits JSON text into keys, strings, numbers, literals and the plain text between them.
/// </summary>
public static class JsonTokenizer
{
    private static readonly SearchValues<char> NumberChars = SearchValues.Create("0123456789+-.eE");

    public static bool LooksLikeJson(string text) => text.AsSpan().TrimStart() is ['{' or '[', ..];

    public static IEnumerable<JsonToken> Tokenize(string text)
    {
        int plainStart = 0;
        int i = 0;

        while (i < text.Length)
        {
            (int end, JsonTokenKind kind) = text[i] switch
            {
                '"' => ReadString(text, i),
                var c when char.IsAsciiDigit(c) || (c == '-' && i + 1 < text.Length && char.IsAsciiDigit(text[i + 1])) => (
                    ReadNumber(text, i),
                    JsonTokenKind.Number),
                var c when char.IsAsciiLetterLower(c) => ReadWord(text, i),
                _ => (i + 1, JsonTokenKind.Plain)
            };

            if (kind != JsonTokenKind.Plain)
            {
                if (plainStart < i)
                {
                    yield return new JsonToken(text[plainStart..i], JsonTokenKind.Plain);
                }

                yield return new JsonToken(text[i..end], kind);
                plainStart = end;
            }

            i = end;
        }

        if (plainStart < text.Length)
        {
            yield return new JsonToken(text[plainStart..], JsonTokenKind.Plain);
        }
    }

    private static (int End, JsonTokenKind Kind) ReadString(string text, int start)
    {
        int end = start + 1;
        while (end < text.Length && text[end] != '"')
        {
            end += text[end] == '\\' ? 2 : 1;
        }

        end = Math.Min(end + 1, text.Length);
        return (end, text.AsSpan(end).TrimStart() is [':', ..] ? JsonTokenKind.Key : JsonTokenKind.String);
    }

    private static int ReadNumber(string text, int start)
    {
        int length = text.AsSpan(start).IndexOfAnyExcept(NumberChars);
        return length < 0 ? text.Length : start + length;
    }

    private static (int End, JsonTokenKind Kind) ReadWord(string text, int start)
    {
        ReadOnlySpan<char> rest = text.AsSpan(start);
        int length = rest.IndexOfAnyExceptInRange('a', 'z');
        ReadOnlySpan<char> word = length < 0 ? rest : rest[..length];
        return (start + word.Length, word is "true" or "false" or "null" ? JsonTokenKind.Literal : JsonTokenKind.Plain);
    }
}
