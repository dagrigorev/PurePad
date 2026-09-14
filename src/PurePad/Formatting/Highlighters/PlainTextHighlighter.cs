namespace PurePad.Formatting.Highlighters;

/// <summary>
/// Null Object highlighter: emits no tokens, so everything renders in the default
/// colour. Used for unknown file types and when colourising is switched off, which
/// lets callers avoid null checks and special cases.
/// </summary>
public sealed class PlainTextHighlighter : ISyntaxHighlighter
{
    public static readonly PlainTextHighlighter Instance = new();

    private PlainTextHighlighter()
    {
    }

    public IEnumerable<TextToken> Tokenize(string text) => Array.Empty<TextToken>();
}
