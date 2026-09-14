namespace PurePad.Formatting;

/// <summary>
/// Abstraction over the set of supported languages and the rules for resolving one.
/// Consumers depend on this interface, not a concrete registry (Dependency Inversion),
/// which keeps them testable and open to alternative catalogues.
/// </summary>
public interface ILanguageCatalog
{
    /// <summary>All registered languages, excluding <see cref="PlainText"/>, in menu order.</summary>
    IReadOnlyList<LanguageDefinition> Languages { get; }

    /// <summary>The fallback plain-text language (Null Object highlighter/formatter).</summary>
    LanguageDefinition PlainText { get; }

    /// <summary>
    /// Resolve a language from a file extension (with or without leading dot).
    /// Returns <see cref="PlainText"/> when nothing matches.
    /// </summary>
    LanguageDefinition ResolveByExtension(string? extension);

    /// <summary>
    /// Resolve a language by its <see cref="LanguageDefinition.Id"/>.
    /// Returns <see cref="PlainText"/> when the id is unknown.
    /// </summary>
    LanguageDefinition ResolveById(string? id);
}
