namespace PurePad.Formatting.Highlighters;

/// <summary>
/// A single-pass, allocation-light scanner that classifies JSON text. It is tolerant
/// of malformed input: it never throws, it simply colours whatever it recognises and
/// treats the rest as plain text.
/// </summary>
public sealed class JsonSyntaxHighlighter : ISyntaxHighlighter
{
    public IEnumerable<TextToken> Tokenize(string text)
    {
        var tokens = new List<TextToken>();
        int i = 0;
        int n = text.Length;

        while (i < n)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            switch (c)
            {
                case '"':
                    int start = i;
                    int end = ScanString(text, i);
                    // A string is a property name when the next significant char is ':'.
                    var kind = NextSignificantChar(text, end) == ':'
                        ? TokenKind.Property
                        : TokenKind.String;
                    tokens.Add(new TextToken(start, end - start, kind));
                    i = end;
                    break;

                case '{':
                case '}':
                case '[':
                case ']':
                case ':':
                case ',':
                    tokens.Add(new TextToken(i, 1, TokenKind.Punctuation));
                    i++;
                    break;

                default:
                    if (c == '-' || char.IsDigit(c))
                    {
                        int numEnd = ScanNumber(text, i);
                        tokens.Add(new TextToken(i, numEnd - i, TokenKind.Number));
                        i = numEnd;
                    }
                    else if (TryMatchWord(text, i, "true", out int te) ||
                             TryMatchWord(text, i, "false", out te))
                    {
                        tokens.Add(new TextToken(i, te - i, TokenKind.Boolean));
                        i = te;
                    }
                    else if (TryMatchWord(text, i, "null", out int ne))
                    {
                        tokens.Add(new TextToken(i, ne - i, TokenKind.Null));
                        i = ne;
                    }
                    else
                    {
                        i++;
                    }
                    break;
            }
        }

        return tokens;
    }

    /// <summary>Returns the index just past the closing quote of the string starting at <paramref name="quote"/>.</summary>
    private static int ScanString(string text, int quote)
    {
        int i = quote + 1;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '\\')
            {
                i += 2; // skip the escaped character
                continue;
            }

            if (c == '"')
            {
                return i + 1;
            }

            i++;
        }

        return i; // unterminated string: consume to end of text
    }

    private static int ScanNumber(string text, int start)
    {
        int i = start;
        if (i < text.Length && text[i] == '-') i++;
        while (i < text.Length && (char.IsDigit(text[i]) || text[i] is '.' or 'e' or 'E' or '+' or '-'))
        {
            i++;
        }

        return i;
    }

    private static char NextSignificantChar(string text, int from)
    {
        for (int i = from; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return text[i];
            }
        }

        return '\0';
    }

    private static bool TryMatchWord(string text, int start, string word, out int end)
    {
        end = start + word.Length;
        if (end > text.Length)
        {
            end = start;
            return false;
        }

        for (int k = 0; k < word.Length; k++)
        {
            if (text[start + k] != word[k])
            {
                end = start;
                return false;
            }
        }

        return true;
    }
}
