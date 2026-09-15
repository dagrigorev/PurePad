using PurePad.Formatting.Checkers;
using PurePad.Formatting.Formatters;
using PurePad.Formatting.Highlighters;
using PurePad.Formatting.Profiles;

namespace PurePad.Formatting;

/// <summary>
/// Default <see cref="ILanguageCatalog"/>: a registry that maps file extensions to the
/// language definitions PurePad ships with. New languages are added by extending
/// <see cref="BuildLanguages"/>; nothing else needs to change (Open/Closed).
/// </summary>
public sealed class LanguageCatalog : ILanguageCatalog
{
    private readonly List<LanguageDefinition> _builtIns;
    private List<LanguageDefinition> _languages = new();
    private Dictionary<string, LanguageDefinition> _byExtension = new();
    private Dictionary<string, LanguageDefinition> _byId = new();

    public LanguageCatalog(IEnumerable<LanguageProfile>? profiles = null)
    {
        PlainText = new LanguageDefinition(
            id: "plain",
            displayName: "Plain Text",
            extensions: Array.Empty<string>(),
            highlighter: PlainTextHighlighter.Instance,
            formatter: NullFormatter.Instance,
            checker: NullSyntaxChecker.Instance);

        _builtIns = BuildLanguages();
        Rebuild(profiles ?? Array.Empty<LanguageProfile>());
    }

    /// <summary>Rebuild the registry after the user edits their language profiles.</summary>
    public void ReloadProfiles(IEnumerable<LanguageProfile> profiles) => Rebuild(profiles);

    private void Rebuild(IEnumerable<LanguageProfile> profiles)
    {
        // Built-ins first, then profile languages so a profile can override an extension.
        var languages = new List<LanguageDefinition>(_builtIns);
        foreach (LanguageProfile profile in profiles)
        {
            if (profile.Extensions.Count == 0)
            {
                continue;
            }

            try
            {
                languages.Add(LanguageProfileFactory.Create(profile));
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                // Skip a malformed profile rather than failing the whole catalogue.
            }
        }

        var byId = new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase) { [PlainText.Id] = PlainText };
        var byExtension = new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (LanguageDefinition language in languages)
        {
            byId[language.Id] = language;
            foreach (string ext in language.Extensions)
            {
                byExtension[ext] = language; // later (profile) wins
            }
        }

        _languages = languages;
        _byId = byId;
        _byExtension = byExtension;
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

        var javascript = new LanguageDefinition(
            id: "javascript",
            displayName: "JavaScript / TypeScript",
            extensions: new[] { ".js", ".mjs", ".cjs", ".jsx", ".ts", ".tsx" },
            highlighter: new CodeSyntaxHighlighter(new CodeSyntaxOptions
            {
                Keywords = CodeKeywords.JavaScript,
                RegexLiterals = true,
                TemplateStrings = true,
            }),
            formatter: NullFormatter.Instance,
            checker: NullSyntaxChecker.Instance,
            completionWords: CodeKeywords.JavaScript);

        var bash = new LanguageDefinition(
            id: "bash",
            displayName: "Shell / Bash",
            extensions: new[] { ".sh", ".bash", ".zsh", ".ksh" },
            highlighter: new CodeSyntaxHighlighter(new CodeSyntaxOptions
            {
                Keywords = CodeKeywords.Bash,
                LineComments = new[] { "#" },
                BlockComment = null,
                VariableSigil = '$',
            }),
            formatter: NullFormatter.Instance,
            checker: NullSyntaxChecker.Instance,
            completionWords: CodeKeywords.Bash);

        var powershell = new LanguageDefinition(
            id: "powershell",
            displayName: "PowerShell",
            extensions: new[] { ".ps1", ".psm1", ".psd1" },
            highlighter: new CodeSyntaxHighlighter(new CodeSyntaxOptions
            {
                Keywords = CodeKeywords.PowerShell,
                LineComments = new[] { "#" },
                BlockComment = ("<#", "#>"),
                VariableSigil = '$',
                CaseInsensitiveKeywords = true,
            }),
            formatter: NullFormatter.Instance,
            checker: NullSyntaxChecker.Instance,
            completionWords: CodeKeywords.PowerShell);

        var batch = new LanguageDefinition(
            id: "batch",
            displayName: "Batch (cmd)",
            extensions: new[] { ".cmd", ".bat" },
            highlighter: new CodeSyntaxHighlighter(new CodeSyntaxOptions
            {
                Keywords = CodeKeywords.Cmd,
                LineComments = new[] { "::" },
                BlockComment = null,
                VariableSigil = '%',
                RemLineComments = true,
                CaseInsensitiveKeywords = true,
            }),
            formatter: NullFormatter.Instance,
            checker: NullSyntaxChecker.Instance,
            completionWords: CodeKeywords.Cmd);

        var sql = new LanguageDefinition(
            id: "sql",
            displayName: "SQL",
            extensions: new[] { ".sql", ".ddl", ".dml", ".pgsql", ".mysql" },
            highlighter: new CodeSyntaxHighlighter(new CodeSyntaxOptions
            {
                Keywords = CodeKeywords.Sql,
                LineComments = new[] { "--" },
                BlockComment = ("/*", "*/"),
                VariableSigil = '@',           // T-SQL / MySQL @variables
                CaseInsensitiveKeywords = true,
            }),
            formatter: NullFormatter.Instance,
            checker: NullSyntaxChecker.Instance,
            completionWords: CodeKeywords.Sql);

        var code = new LanguageDefinition(
            id: "code",
            displayName: "Source Code",
            extensions: new[]
            {
                ".cs", ".java", ".c", ".cpp", ".cc", ".h", ".hpp",
                ".css", ".go", ".rs", ".php", ".py", ".rb",
            },
            highlighter: new CodeSyntaxHighlighter(CodeKeywords.CFamily, hashLineComments: true),
            formatter: NullFormatter.Instance,
            checker: NullSyntaxChecker.Instance,
            completionWords: CodeKeywords.CFamily);

        return new List<LanguageDefinition> { json, xml, markdown, javascript, bash, powershell, batch, sql, code };
    }
}
