namespace PurePad.Formatting;

/// <summary>
/// Strategy for pretty-printing / reformatting a whole document (e.g. re-indenting
/// JSON or XML). Separate from <see cref="ISyntaxHighlighter"/> because colouring and
/// reformatting are independent concerns with different lifetimes (Interface Segregation).
/// </summary>
public interface ITextFormatter
{
    /// <summary>Whether this formatter can actually transform text. A no-op formatter reports false.</summary>
    bool CanFormat { get; }

    /// <summary>
    /// Return a reformatted copy of <paramref name="text"/>.
    /// </summary>
    /// <exception cref="TextFormatException">
    /// Thrown when the input is malformed and cannot be reformatted.
    /// </exception>
    string Format(string text);
}

/// <summary>Raised by an <see cref="ITextFormatter"/> when input cannot be reformatted.</summary>
public sealed class TextFormatException : Exception
{
    public TextFormatException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
