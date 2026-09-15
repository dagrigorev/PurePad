using System.Drawing;

namespace PurePad.Formatting;

/// <summary>A font request from a language profile (applied when a file of that type is shown).</summary>
public sealed record FontSpec(string Family, float Size, FontStyle Style);

/// <summary>Language-server settings resolved from a profile (empty command means "no server").</summary>
public sealed record LspSettings(string Command, IReadOnlyList<string> Args, string LanguageId, IReadOnlyList<string> RootMarkers);

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
        ISyntaxChecker checker,
        IReadOnlyDictionary<TokenKind, Color>? colorOverrides = null,
        FontSpec? font = null,
        IReadOnlyList<string>? completionWords = null,
        LspSettings? lsp = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Extensions = extensions ?? throw new ArgumentNullException(nameof(extensions));
        Highlighter = highlighter ?? throw new ArgumentNullException(nameof(highlighter));
        Formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        Checker = checker ?? throw new ArgumentNullException(nameof(checker));
        ColorOverrides = colorOverrides;
        Font = font;
        CompletionWords = completionWords ?? Array.Empty<string>();
        Lsp = lsp;
    }

    /// <summary>Static words offered as completions for this language (keywords + profile completions).</summary>
    public IReadOnlyList<string> CompletionWords { get; }

    /// <summary>Language-server settings for this file type, or null when none is configured.</summary>
    public LspSettings? Lsp { get; }

    /// <summary>Per-token colour overrides from a profile, merged onto the active theme, or null.</summary>
    public IReadOnlyDictionary<TokenKind, Color>? ColorOverrides { get; }

    /// <summary>Font to apply when a file of this type is shown, or null to keep the global font.</summary>
    public FontSpec? Font { get; }

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
