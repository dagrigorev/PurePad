namespace PurePad.Formatting.Highlighters;

/// <summary>
/// A general-purpose highlighter for C-family source (C#, JavaScript, Java, C/C++, CSS
/// and friends). It colours line and block comments, quoted strings, numbers and a
/// configurable keyword set. The keyword list is injected so one scanner serves many
/// languages (Open/Closed) without a subclass per language.
/// </summary>
public sealed class CodeSyntaxHighlighter : ISyntaxHighlighter
{
    private readonly HashSet<string> _keywords;
    private readonly bool _hashLineComments;

    public CodeSyntaxHighlighter(IEnumerable<string> keywords, bool hashLineComments = false)
    {
        _keywords = new HashSet<string>(keywords, StringComparer.Ordinal);
        _hashLineComments = hashLineComments;
    }

    public IEnumerable<TextToken> Tokenize(string text)
    {
        var tokens = new List<TextToken>();
        int i = 0;
        int n = text.Length;

        while (i < n)
        {
            char c = text[i];

            // Block comment /* ... */
            if (c == '/' && i + 1 < n && text[i + 1] == '*')
            {
                int close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                int end = close < 0 ? n : close + 2;
                tokens.Add(new TextToken(i, end - i, TokenKind.Comment));
                i = end;
                continue;
            }

            // Line comment // ... (and optionally # ... for scripting languages)
            if ((c == '/' && i + 1 < n && text[i + 1] == '/') || (_hashLineComments && c == '#'))
            {
                int end = EndOfLine(text, i);
                tokens.Add(new TextToken(i, end - i, TokenKind.Comment));
                i = end;
                continue;
            }

            // Strings: "double", 'single' and `template`.
            if (c is '"' or '\'' or '`')
            {
                int end = ScanString(text, i, c);
                tokens.Add(new TextToken(i, end - i, TokenKind.String));
                i = end;
                continue;
            }

            // Numbers (including hex and decimals).
            if (char.IsDigit(c))
            {
                int end = ScanNumber(text, i);
                tokens.Add(new TextToken(i, end - i, TokenKind.Number));
                i = end;
                continue;
            }

            // Identifiers / keywords.
            if (char.IsLetter(c) || c == '_')
            {
                int end = i + 1;
                while (end < n && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
                {
                    end++;
                }

                string word = text.Substring(i, end - i);
                if (_keywords.Contains(word))
                {
                    tokens.Add(new TextToken(i, end - i, TokenKind.Keyword));
                }

                i = end;
                continue;
            }

            i++;
        }

        return tokens;
    }

    private static int ScanString(string text, int start, char quote)
    {
        int i = start + 1;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '\\')
            {
                i += 2;
                continue;
            }

            if (c == quote)
            {
                return i + 1;
            }

            // Do not let a non-template string run past its own line.
            if (quote != '`' && (c == '\n' || c == '\r'))
            {
                return i;
            }

            i++;
        }

        return i;
    }

    private static int ScanNumber(string text, int start)
    {
        int i = start;
        if (text[i] == '0' && i + 1 < text.Length && (text[i + 1] is 'x' or 'X'))
        {
            i += 2;
            while (i < text.Length && Uri.IsHexDigit(text[i])) i++;
            return i;
        }

        while (i < text.Length && (char.IsDigit(text[i]) || text[i] is '.' or 'e' or 'E' or '_' or 'f' or 'F' or 'd' or 'D' or 'L' or 'l'))
        {
            i++;
        }

        return i;
    }

    private static int EndOfLine(string text, int start)
    {
        int i = start;
        while (i < text.Length && text[i] != '\n' && text[i] != '\r')
        {
            i++;
        }

        return i;
    }
}
