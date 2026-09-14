namespace PurePad.Formatting;

/// <summary>
/// The semantic category of a span of text produced by a syntax highlighter.
/// Highlighters classify text into these kinds; a <see cref="SyntaxTheme"/> maps
/// each kind to a colour. Keeping the vocabulary in one enum lets every language
/// share a single theme (Open/Closed: add a language without touching the theme).
/// </summary>
public enum TokenKind
{
    /// <summary>Ordinary text rendered in the default foreground colour.</summary>
    PlainText,
    Keyword,
    String,
    Number,
    Comment,
    Operator,
    Punctuation,
    /// <summary>An object member name (e.g. a JSON property key).</summary>
    Property,
    Boolean,
    Null,
    /// <summary>A markup element/tag name including its angle brackets.</summary>
    Tag,
    AttributeName,
    AttributeValue,
    /// <summary>A Markdown heading line.</summary>
    Heading,
    /// <summary>Markdown emphasis (bold/italic) or inline code.</summary>
    Emphasis,
}
