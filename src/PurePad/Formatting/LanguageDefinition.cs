namespace PurePad.Formatting;

/// <summary>
/// Bundles everything the editor needs to treat a document as a particular language:
/// a display name, the file extensions it owns, its highlighter and its formatter.
/// Grouping these together means adding a language is a single registration
/// (Open/Closed) rather than edits scattered across factories.
/// </summary>
public sealed class LanguageDefinition
{
    public LanguageDefinition(
        string id,
        string displayName,
        IReadOnlyList<string> extensions,
        ISyntaxHighlighter highlighter,
        ITextFormatter formatter,
        ISyntaxChecker checker)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Extensions = extensions ?? throw new ArgumentNullException(nameof(extensions));
        Highlighter = highlighter ?? throw new ArgumentNullException(nameof(highlighter));
        Formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        Checker = checker ?? throw new ArgumentNullException(nameof(checker));
    }

    /// <summary>Stable identifier used to persist/select the language (e.g. "json").</summary>
    public string Id { get; }

    /// <summary>Human-readable name shown in the Format menu (e.g. "JSON").</summary>
    public string DisplayName { get; }

    /// <summary>Lower-case file extensions including the leading dot (e.g. ".json").</summary>
    public IReadOnlyList<string> Extensions { get; }

    public ISyntaxHighlighter Highlighter { get; }

    public ITextFormatter Formatter { get; }

    public ISyntaxChecker Checker { get; }
}
