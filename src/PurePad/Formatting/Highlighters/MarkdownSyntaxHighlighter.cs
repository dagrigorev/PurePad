namespace PurePad.Formatting.Highlighters;

/// <summary>
/// A line-oriented Markdown highlighter: it colours ATX headings, fenced/inline code,
/// and <c>**bold**</c> / <c>*italic*</c> emphasis. It works line by line, which keeps
/// the scanner simple and predictable.
/// </summary>
public sealed class MarkdownSyntaxHighlighter : ISyntaxHighlighter
{
    public IEnumerable<TextToken> Tokenize(string text)
    {
        var tokens = new List<TextToken>();
        int lineStart = 0;
        bool inFence = false;

        while (lineStart <= text.Length)
        {
            int lineEnd = IndexOfLineEnd(text, lineStart);
            int length = lineEnd - lineStart;
            string line = text.Substring(lineStart, length);
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                tokens.Add(new TextToken(lineStart, length, TokenKind.Emphasis));
                inFence = !inFence;
            }
            else if (inFence)
            {
                if (length > 0)
                {
                    tokens.Add(new TextToken(lineStart, length, TokenKind.String));
                }
            }
            else if (trimmed.StartsWith('#'))
            {
                tokens.Add(new TextToken(lineStart, length, TokenKind.Heading));
            }
            else
            {
                AddInlineSpans(text, lineStart, lineEnd, tokens);
            }

            if (lineEnd >= text.Length)
            {
                break;
            }

            lineStart = SkipLineBreak(text, lineEnd);
        }

        return tokens;
    }

    /// <summary>Colours inline <c>`code`</c> and <c>*</c>/<c>_</c> emphasis within one line.</summary>
    private static void AddInlineSpans(string text, int from, int to, List<TextToken> tokens)
    {
        int i = from;
        while (i < to)
        {
            char c = text[i];

            if (c == '`')
            {
                int end = text.IndexOf('`', i + 1);
                if (end < 0 || end >= to) { i++; continue; }
                tokens.Add(new TextToken(i, end - i + 1, TokenKind.String));
                i = end + 1;
                continue;
            }

            if (c is '*' or '_')
            {
                int runStart = i;
                while (i < to && text[i] == c) i++;
                int markerLen = i - runStart;
                int close = FindClosingRun(text, i, to, c, markerLen);
                if (close < 0) { continue; }
                tokens.Add(new TextToken(runStart, close + markerLen - runStart, TokenKind.Emphasis));
                i = close + markerLen;
                continue;
            }

            i++;
        }
    }

    private static int FindClosingRun(string text, int from, int to, char marker, int markerLen)
    {
        for (int i = from; i + markerLen <= to; i++)
        {
            bool match = true;
            for (int k = 0; k < markerLen; k++)
            {
                if (text[i + k] != marker) { match = false; break; }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfLineEnd(string text, int start)
    {
        int i = start;
        while (i < text.Length && text[i] != '\n' && text[i] != '\r')
        {
            i++;
        }

        return i;
    }

    private static int SkipLineBreak(string text, int lineEnd)
    {
        if (lineEnd < text.Length && text[lineEnd] == '\r' && lineEnd + 1 < text.Length && text[lineEnd + 1] == '\n')
        {
            return lineEnd + 2;
        }

        return lineEnd + 1;
    }
}
