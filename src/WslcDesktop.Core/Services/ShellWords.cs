using System.Text;

namespace WslcDesktop.Services;

/// <summary>
///     Splits and joins command lines the way a POSIX shell quotes words, as container commands are written.
/// </summary>
public static class ShellWords
{
    private const string NeedsQuoting = " \t\n'\"\\;&|$`<>()*?#";

    /// <summary>
    ///     The words of a command line, with their quotes and escapes removed.
    /// </summary>
    /// <param name="text">A command line quoted as in a POSIX shell.</param>
    /// <exception cref="FormatException">A quote is not closed.</exception>
    public static List<string> Split(string text)
    {
        List<string> words = [];
        var word = new StringBuilder();
        bool inWord = false;
        char? quote = null;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quote == '\'')
            {
                if (c == '\'')
                {
                    quote = null;
                }
                else
                {
                    word.Append(c);
                }

                continue;
            }

            if (quote == '"')
            {
                if (c == '"')
                {
                    quote = null;
                }
                else if (c == '\\' && i + 1 < text.Length && text[i + 1] is '"' or '\\')
                {
                    word.Append(text[++i]);
                }
                else
                {
                    word.Append(c);
                }

                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (inWord)
                {
                    words.Add(word.ToString());
                    word.Clear();
                    inWord = false;
                }

                continue;
            }

            inWord = true;
            if (c is '\'' or '"')
            {
                quote = c;
            }
            else if (c == '\\' && i + 1 < text.Length)
            {
                word.Append(text[++i]);
            }
            else
            {
                word.Append(c);
            }
        }

        if (quote is { } open)
        {
            throw new FormatException($"A {open} quote is not closed.");
        }

        if (inWord)
        {
            words.Add(word.ToString());
        }

        return words;
    }

    /// <summary>
    ///     A command line that <see cref="Split" /> turns back into the same words.
    /// </summary>
    public static string Join(IEnumerable<string> words) => string.Join(' ', words.Select(Quote));

    private static string Quote(string word) =>
        word.Length == 0 ? "''"
        : word.AsSpan().IndexOfAny(NeedsQuoting) < 0 ? word
        : $"'{word.Replace("'", @"'\''", StringComparison.Ordinal)}'";
}
