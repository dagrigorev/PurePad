namespace PurePad.Formatting;

/// <summary>
/// Strategy for classifying document text into coloured <see cref="TextToken"/> spans.
/// Implementations must be pure and side-effect free: given the same text they return
/// the same tokens, they never touch the UI, and they never throw on malformed input
/// (a highlighter degrades gracefully rather than failing the editor).
/// </summary>
public interface ISyntaxHighlighter
{
    /// <summary>
    /// Produce the coloured spans for <paramref name="text"/>. Only non-default spans
    /// need be returned; any character not covered is rendered in the default colour.
    /// Returned tokens must be within bounds and should be ordered by <see cref="TextToken.Start"/>.
    /// </summary>
    IEnumerable<TextToken> Tokenize(string text);
}
