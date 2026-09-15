namespace PurePad.Formatting;

/// <summary>
/// Optionally implemented by a highlighter to declare its block-comment delimiters, so the editor
/// can colour multi-line block comments correctly (it must know where a comment continues across
/// lines). Highlighters without block comments need not implement this.
/// </summary>
public interface IBlockCommentDelimiters
{
    /// <summary>The open/close delimiters (e.g. <c>("/*", "*/")</c> or <c>("&lt;#", "#&gt;")</c>), or null.</summary>
    (string Open, string Close)? BlockComment { get; }
}
