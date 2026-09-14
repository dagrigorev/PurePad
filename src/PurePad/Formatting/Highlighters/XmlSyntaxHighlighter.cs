namespace PurePad.Formatting.Highlighters;

/// <summary>
/// Scans XML/HTML markup, colouring comments, element tags, attribute names and
/// attribute values. Text content between tags is left as plain text. Like the other
/// highlighters it is fault-tolerant and never throws on malformed markup.
/// </summary>
public sealed class XmlSyntaxHighlighter : ISyntaxHighlighter
{
    public IEnumerable<TextToken> Tokenize(string text)
    {
        var tokens = new List<TextToken>();
        int i = 0;
        int n = text.Length;

        while (i < n)
        {
            int lt = text.IndexOf('<', i);
            if (lt < 0)
            {
                break; // remaining text is element content
            }

            if (Matches(text, lt, "<!--"))
            {
                int close = text.IndexOf("-->", lt + 4, StringComparison.Ordinal);
                int end = close < 0 ? n : close + 3;
                tokens.Add(new TextToken(lt, end - lt, TokenKind.Comment));
                i = end;
                continue;
            }

            int gt = text.IndexOf('>', lt + 1);
            int tagEnd = gt < 0 ? n : gt + 1;
            TokenizeTag(text, lt, tagEnd, tokens);
            i = tagEnd;
        }

        return tokens;
    }

    /// <summary>Classifies the contents of a single <c>&lt;...&gt;</c> tag.</summary>
    private static void TokenizeTag(string text, int start, int end, List<TextToken> tokens)
    {
        int i = start;

        // Opening punctuation and the element name, e.g. "<div", "</div", "<?xml".
        int nameStart = i;
        i++; // consume '<'
        if (i < end && (text[i] == '/' || text[i] == '?' || text[i] == '!'))
        {
            i++;
        }

        while (i < end && (char.IsLetterOrDigit(text[i]) || text[i] is '_' or '-' or ':' or '.'))
        {
            i++;
        }

        tokens.Add(new TextToken(nameStart, i - nameStart, TokenKind.Tag));

        // Attributes: name [= "value"] pairs until the closing bracket.
        while (i < end)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c is '>' or '/' or '?')
            {
                tokens.Add(new TextToken(i, 1, TokenKind.Tag));
                i++;
                continue;
            }

            if (c == '=')
            {
                tokens.Add(new TextToken(i, 1, TokenKind.Operator));
                i++;
                continue;
            }

            if (c is '"' or '\'')
            {
                int valStart = i;
                int valEnd = ScanQuoted(text, i, end);
                tokens.Add(new TextToken(valStart, valEnd - valStart, TokenKind.AttributeValue));
                i = valEnd;
                continue;
            }

            // Attribute name run.
            int attrStart = i;
            while (i < end && !char.IsWhiteSpace(text[i]) && text[i] is not ('=' or '>' or '/' or '"' or '\''))
            {
                i++;
            }

            if (i > attrStart)
            {
                tokens.Add(new TextToken(attrStart, i - attrStart, TokenKind.AttributeName));
            }
            else
            {
                i++; // guarantee forward progress on stray characters
            }
        }
    }

    private static int ScanQuoted(string text, int quote, int limit)
    {
        char q = text[quote];
        int i = quote + 1;
        while (i < limit && text[i] != q)
        {
            i++;
        }

        return i < limit ? i + 1 : limit;
    }

    private static bool Matches(string text, int at, string token)
    {
        if (at + token.Length > text.Length)
        {
            return false;
        }

        return string.CompareOrdinal(text, at, token, 0, token.Length) == 0;
    }
}
