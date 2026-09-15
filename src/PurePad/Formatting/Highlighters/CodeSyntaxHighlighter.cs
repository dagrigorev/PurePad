namespace PurePad.Formatting.Highlighters;

/// <summary>Configuration for <see cref="CodeSyntaxHighlighter"/> so one scanner serves many languages.</summary>
public sealed class CodeSyntaxOptions
{
    public IEnumerable<string> Keywords { get; init; } = Array.Empty<string>();

    /// <summary>Prefixes that begin a line comment (e.g. <c>//</c>, <c>#</c>, <c>::</c>).</summary>
    public string[] LineComments { get; init; } = { "//" };

    /// <summary>Block-comment delimiters, or null when the language has none.</summary>
    public (string Open, string Close)? BlockComment { get; init; } = ("/*", "*/");

    /// <summary>Recognise JavaScript-style <c>/regex/flags</c> literals.</summary>
    public bool RegexLiterals { get; init; }

    /// <summary>Variable sigil to colour: <c>'$'</c> (shell/PowerShell) or <c>'%'</c> (batch), else <c>'\0'</c>.</summary>
    public char VariableSigil { get; init; }

    /// <summary>Batch: a <c>rem</c> word at the start of a line begins a comment.</summary>
    public bool RemLineComments { get; init; }

    /// <summary>Match keywords case-insensitively (PowerShell, batch).</summary>
    public bool CaseInsensitiveKeywords { get; init; }

    /// <summary>Backtick template literals that may span multiple lines (JavaScript).</summary>
    public bool TemplateStrings { get; init; }
}

/// <summary>
/// A configurable highlighter for C-family and script languages. It colours line and block
/// comments, quoted strings, numbers, an injected keyword set, and — per options — regex
/// literals and <c>$</c>/<c>%</c> variables. The vocabulary and comment/variable syntax are
/// injected (Open/Closed) so a single scanner serves C#, JS, Bash, PowerShell and batch.
/// </summary>
public sealed class CodeSyntaxHighlighter : ISyntaxHighlighter, IBlockCommentDelimiters, IMultilineTemplate
{
    private readonly HashSet<string> _keywords;
    private readonly CodeSyntaxOptions _options;

    public CodeSyntaxHighlighter(CodeSyntaxOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _keywords = new HashSet<string>(options.Keywords,
            options.CaseInsensitiveKeywords ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }

    /// <summary>Convenience for C-family languages: <c>//</c> (+ optional <c>#</c>) and <c>/* */</c>.</summary>
    public CodeSyntaxHighlighter(IEnumerable<string> keywords, bool hashLineComments = false)
        : this(new CodeSyntaxOptions
        {
            Keywords = keywords,
            LineComments = hashLineComments ? new[] { "//", "#" } : new[] { "//" },
        })
    {
    }

    public (string Open, string Close)? BlockComment => _options.BlockComment;

    public char TemplateQuote => _options.TemplateStrings ? '`' : '\0';

    public IEnumerable<TextToken> Tokenize(string text)
    {
        var tokens = new List<TextToken>();
        int i = 0;
        int n = text.Length;
        bool atLineStart = true;
        TokenKind lastKind = TokenKind.PlainText;
        char lastSignificant = '\0';

        while (i < n)
        {
            char c = text[i];

            if (c == '\n' || c == '\r')
            {
                atLineStart = true;
                i++;
                continue;
            }

            bool wasLineStart = atLineStart;
            if (char.IsWhiteSpace(c))
            {
                i++; // spaces/tabs don't change the "previous significant token" state
                continue;
            }

            atLineStart = false;

            // Block comment (configurable delimiters).
            if (_options.BlockComment is { } block && Matches(text, i, block.Open))
            {
                int close = text.IndexOf(block.Close, i + block.Open.Length, StringComparison.Ordinal);
                int end = close < 0 ? n : close + block.Close.Length;
                tokens.Add(new TextToken(i, end - i, TokenKind.Comment));
                i = end;
                lastKind = TokenKind.Comment;
                continue;
            }

            // Line comments: any configured prefix, plus batch "rem" at line start.
            if (LineCommentHere(text, i, wasLineStart, out int commentLen))
            {
                int end = EndOfLine(text, i);
                tokens.Add(new TextToken(i, end - i, TokenKind.Comment));
                i = end;
                lastKind = TokenKind.Comment;
                _ = commentLen;
                continue;
            }

            // Regex literal (JavaScript) — only where a value is expected.
            if (_options.RegexLiterals && c == '/' && RegexAllowed(lastKind, lastSignificant))
            {
                int end = ScanRegex(text, i);
                if (end > i)
                {
                    tokens.Add(new TextToken(i, end - i, TokenKind.Regex));
                    i = end;
                    lastKind = TokenKind.Regex;
                    lastSignificant = '/';
                    continue;
                }
            }

            // Variables ($name / ${...} / %VAR%).
            if (_options.VariableSigil != '\0' && c == _options.VariableSigil)
            {
                int end = ScanVariable(text, i, EndOfLine(text, i));
                if (end > i + 1)
                {
                    tokens.Add(new TextToken(i, end - i, TokenKind.Variable));
                    i = end;
                    lastKind = TokenKind.Variable;
                    lastSignificant = c;
                    continue;
                }
            }

            // Strings: "double", 'single' and `template`. Interpolated variables are overlaid on top.
            if (c is '"' or '\'' or '`')
            {
                int end = ScanString(text, i, c);
                tokens.Add(new TextToken(i, end - i, TokenKind.String));
                AddStringInterpolations(tokens, text, i, end, c);
                i = end;
                lastKind = TokenKind.String;
                lastSignificant = c;
                continue;
            }

            // Numbers (including hex and decimals).
            if (char.IsDigit(c))
            {
                int end = ScanNumber(text, i);
                tokens.Add(new TextToken(i, end - i, TokenKind.Number));
                i = end;
                lastKind = TokenKind.Number;
                lastSignificant = c;
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
                    lastKind = TokenKind.Keyword;
                }
                else
                {
                    lastKind = TokenKind.PlainText;
                }

                lastSignificant = text[end - 1];
                i = end;
                continue;
            }

            lastSignificant = c;
            lastKind = TokenKind.PlainText;
            i++;
        }

        return tokens;
    }

    private bool LineCommentHere(string text, int i, bool atLineStart, out int length)
    {
        foreach (string prefix in _options.LineComments)
        {
            if (Matches(text, i, prefix))
            {
                length = prefix.Length;
                return true;
            }
        }

        if (_options.RemLineComments && atLineStart && MatchesRemWord(text, i))
        {
            length = 3;
            return true;
        }

        length = 0;
        return false;
    }

    private static bool MatchesRemWord(string text, int i)
    {
        // "rem" (case-insensitive) followed by end-of-line or whitespace.
        if (i + 3 > text.Length)
        {
            return false;
        }

        if (char.ToLowerInvariant(text[i]) != 'r' || char.ToLowerInvariant(text[i + 1]) != 'e' || char.ToLowerInvariant(text[i + 2]) != 'm')
        {
            return false;
        }

        return i + 3 == text.Length || text[i + 3] == ' ' || text[i + 3] == '\t' || text[i + 3] == '\n' || text[i + 3] == '\r';
    }

    private static bool Matches(string text, int i, string token) =>
        i + token.Length <= text.Length && string.CompareOrdinal(text, i, token, 0, token.Length) == 0;

    private static bool RegexAllowed(TokenKind lastKind, char lastSignificant)
    {
        if (lastKind == TokenKind.Keyword)
        {
            return true; // return /x/, typeof /x/, case /x/, etc.
        }

        return lastSignificant is '\0' or '(' or '[' or '{' or ',' or ';' or ':' or '='
            or '!' or '&' or '|' or '?' or '+' or '-' or '*' or '%' or '^' or '~' or '<' or '>';
    }

    private static int ScanRegex(string text, int start)
    {
        int i = start + 1;
        bool inClass = false;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '\n' || c == '\r')
            {
                return start; // no closing slash on this line — not a regex
            }

            if (c == '\\')
            {
                i += 2;
                continue;
            }

            if (c == '[')
            {
                inClass = true;
            }
            else if (c == ']')
            {
                inClass = false;
            }
            else if (c == '/' && !inClass)
            {
                i++;
                while (i < text.Length && char.IsLetter(text[i]))
                {
                    i++; // flags
                }

                return i;
            }

            i++;
        }

        return start;
    }

    /// <summary>Scan one variable at <paramref name="start"/>, not crossing <paramref name="limit"/>.</summary>
    private int ScanVariable(string text, int start, int limit)
    {
        char sigil = _options.VariableSigil;
        int i = start + 1;
        if (i >= limit)
        {
            return start;
        }

        if (sigil == '%')
        {
            if (text[i] == '%') // batch for-loop variable %%i
            {
                i++;
                if (i < limit && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                return i;
            }

            int close = text.IndexOf('%', i);
            return close >= 0 && close < limit ? close + 1 : start;
        }

        // '$': ${...}, $name, or a special like $?, $@, $1
        if (text[i] == '{')
        {
            int close = text.IndexOf('}', i);
            return close >= 0 && close < limit ? close + 1 : start;
        }

        if (char.IsLetterOrDigit(text[i]) || text[i] is '_' or '?' or '@' or '#' or '!' or '*')
        {
            i++;
            while (i < limit && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
            {
                i++;
            }

            return i;
        }

        return start;
    }

    /// <summary>
    /// Overlay <see cref="TokenKind.Variable"/> tokens for interpolations inside a string: <c>$name</c>
    /// / <c>${…}</c> or <c>%VAR%</c> in double quotes, and <c>${…}</c> in a backtick template. Single
    /// quotes are literal (no interpolation). Added after the string token, so they win in rendering.
    /// </summary>
    private void AddStringInterpolations(List<TextToken> tokens, string text, int strStart, int strEnd, char quote)
    {
        bool dollar = quote == '"' && _options.VariableSigil == '$';
        bool percent = quote == '"' && _options.VariableSigil == '%';
        bool template = quote == '`';
        if (!dollar && !percent && !template)
        {
            return;
        }

        int limit = strEnd - 1; // exclude the closing quote
        int i = strStart + 1;
        while (i < limit)
        {
            char c = text[i];
            if (c == '\\')
            {
                i += 2; // escaped char (e.g. \$ in a template)
                continue;
            }

            if (template && c == '$' && i + 1 < limit && text[i + 1] == '{')
            {
                int close = text.IndexOf('}', i + 2);
                int end = close >= 0 && close < limit ? close + 1 : limit;
                tokens.Add(new TextToken(i, end - i, TokenKind.Variable));
                i = end;
                continue;
            }

            if ((dollar && c == '$') || (percent && c == '%'))
            {
                int end = ScanVariable(text, i, limit);
                if (end > i + 1)
                {
                    tokens.Add(new TextToken(i, end - i, TokenKind.Variable));
                    i = end;
                    continue;
                }
            }

            i++;
        }
    }

    private static int ScanString(string text, int start, char quote)
    {
        int i = start + 1;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '\\')
            {
                i += 2; // skip the escaped char (may step one past the end — clamped below)
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

        return Math.Min(i, text.Length); // a trailing backslash can overshoot by one
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
