namespace PurePad.Formatting;

/// <summary>
/// Optionally implemented by a highlighter whose strings can span multiple lines (a JavaScript
/// backtick template literal), so the editor can continue string colouring across line breaks.
/// </summary>
public interface IMultilineTemplate
{
    /// <summary>The multi-line string delimiter (e.g. a backtick), or <c>'\0'</c> when none.</summary>
    char TemplateQuote { get; }
}
