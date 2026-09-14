using PurePad.Formatting.Checkers;
using PurePad.Formatting.Formatters;
using PurePad.Formatting.Highlighters;

namespace PurePad.Formatting;

/// <summary>
/// Default <see cref="ILanguageCatalog"/>: a registry that maps file extensions to the
/// language definitions PurePad ships with. New languages are added by extending
/// <see cref="BuildLanguages"/>; nothing else needs to change (Open/Closed).
/// </summary>
public sealed class LanguageCatalog : ILanguageCatalog
{
    private readonly List<LanguageDefinition> _languages;
    private readonly Dictionary<string, LanguageDefinition> _byExtension;
    private readonly Dictionary<string, LanguageDefinition> _byId;

    public LanguageCatalog()
    {
        PlainText = new LanguageDefinition(
            id: "plain",
            displayName: "Plain Text",
            extensions: Array.Empty<string>(),
            highlighter: PlainTextHighlighter.Instance,
            formatter: NullFormatter.Instance,
            checker: NullSyntaxChecker.Instance);

        _languages = BuildLanguages();

        _byId = new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [PlainText.Id] = PlainText,
        };
        _byExtension = new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var language in _languages)
        {
            _byId[language.Id] = language;
            foreach (var ext in language.Extensions)
            {
                _byExtension[ext] = language;
            }
        }
    }

    public IReadOnlyList<LanguageDefinition> Languages => _languages;

    public LanguageDefinition PlainText { get; }

    public LanguageDefinition ResolveByExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return PlainText;
        }

        string normalized = extension.StartsWith('.') ? extension : "." + extension;
        return _byExtension.TryGetValue(normalized, out var language) ? language : PlainText;
    }

    public LanguageDefinition ResolveById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return PlainText;
        }

        return _byId.TryGetValue(id, out var language) ? language : PlainText;
    }

    private static List<LanguageDefinition> BuildLanguages()
    {
        var json = new LanguageDefinition(
            id: "json",
            displayName: "JSON",
            extensions: new[] { ".json" },
            highlighter: new JsonSyntaxHighlighter(),
            formatter: new JsonFormatter(),
            checker: new JsonSyntaxChecker());

        var xml = new LanguageDefinition(
            id: "xml",
            displayName: "XML / HTML",
            extensions: new[] { ".xml", ".html", ".htm", ".xaml", ".csproj", ".config", ".svg", ".xsd", ".resx" },
            highlighter: new XmlSyntaxHighlighter(),
            formatter: new XmlFormatter(),
            checker: new XmlSyntaxChecker());

        var markdown = new LanguageDefinition(
            id: "markdown",
            displayName: "Markdown",
            extensions: new[] { ".md", ".markdown" },
            highlighter: new MarkdownSyntaxHighlighter(),
            formatter: NullFormatter.Instance,
            checker: NullSyntaxChecker.Instance);

        var code = new LanguageDefinition(
            id: "code",
            displayName: "Source Code",
            extensions: new[]
            {
                ".cs", ".js", ".ts", ".java", ".c", ".cpp", ".cc", ".h", ".hpp",
                ".css", ".go", ".rs", ".php", ".py", ".rb", ".sql", ".ps1", ".sh",
            },
            highlighter: new CodeSyntaxHighlighter(CodeKeywords.CFamily, hashLineComments: true),
            formatter: NullFormatter.Instance,
            checker: NullSyntaxChecker.Instance);

        return new List<LanguageDefinition> { json, xml, markdown, code };
    }
}
