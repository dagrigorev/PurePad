using System.Text.Json;
using System.Text.Json.Serialization;
using PurePad.Formatting.Profiles;

namespace PurePad.Configuration;

/// <summary>Container for the user's language profiles, matching the on-disk <c>languages.json</c>.</summary>
public sealed class LanguageProfileFile
{
    [JsonPropertyName("//")]
    public string? Description { get; set; }

    public List<LanguageProfile> Profiles { get; set; } = new();
}

/// <summary>
/// Reads and writes <c>languages.json</c> — the user-editable file that describes how each file type
/// is coloured, formatted and displayed. On first run it writes a documented default with worked
/// examples so the format is discoverable from inside the editor.
/// </summary>
public sealed class LanguageProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public LanguageProfileStore(string path) => Path = path;

    public string Path { get; }

    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PurePad", "languages.json");

    /// <summary>Load the profiles, creating a documented default file if none exists.</summary>
    public IReadOnlyList<LanguageProfile> Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                EnsureDefaultFile();
            }

            string json = File.ReadAllText(Path);
            var file = JsonSerializer.Deserialize<LanguageProfileFile>(json, Options);
            return file?.Profiles ?? new List<LanguageProfile>();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new List<LanguageProfile>(); // fall back to built-in languages
        }
    }

    /// <summary>Write the default file (with examples) if it does not already exist.</summary>
    public void EnsureDefaultFile()
    {
        if (File.Exists(Path))
        {
            return;
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(BuildDefault(), Options));
    }

    private static LanguageProfileFile BuildDefault() => new()
    {
        Description =
            "PurePad language profiles. Each profile binds file 'extensions' to syntax colouring, a " +
            "font, beautifier rules, bracket-balance checking, regex problem rules and completions. " +
            "Edit a profile or add your own, then Format > Reload Language Profiles. " +
            "syntax.variableSigil is \"$\" or \"%\"; colors keys are token kinds (Keyword, String, " +
            "Number, Comment, Variable, Regex, ...) mapped to hex. 'brackets' are pairs checked for " +
            "balance; 'diagnostics' are {pattern, message, severity: error|warning|info} rules shown " +
            "in the Problems panel; 'completions' are extra suggestion words. Optional 'lsp' connects " +
            "a Language Server that is installed on PATH for real diagnostics: " +
            "{ enabled, command, args, languageId, rootMarkers }. Regex backslashes must be doubled " +
            "in JSON, e.g. \"\\\\bTODO\\\\b\".",
        Profiles = new List<LanguageProfile>
        {
            new()
            {
                Id = "python-lsp",
                Name = "Python (LSP template)",
                Extensions = new List<string>(), // add ".py" to enable; empty means this template is inert
                Syntax = new SyntaxProfile { LineComments = new List<string> { "#" }, BlockComment = null },
                Lsp = new LspProfile
                {
                    Enabled = false, // set true and add ".py" above once pyright is installed
                    Command = "pyright-langserver",
                    Args = new List<string> { "--stdio" },
                    LanguageId = "python",
                    RootMarkers = new List<string> { "pyproject.toml", "setup.py", ".git" },
                },
            },
            new()
            {
                Id = "ini",
                Name = "INI / Config",
                Extensions = new List<string> { ".ini", ".conf", ".cfg" },
                Syntax = new SyntaxProfile
                {
                    Keywords = new List<string> { "true", "false", "on", "off", "yes", "no" },
                    LineComments = new List<string> { ";", "#" },
                    BlockComment = null,
                    CaseInsensitiveKeywords = true,
                },
                Colors = new Dictionary<string, string> { ["Comment"] = "#6A9955", ["Keyword"] = "#569CD6" },
                Format = new FormatProfile { ReindentByBrackets = false, TrimTrailingWhitespace = true },
            },
            new()
            {
                Id = "toml",
                Name = "TOML",
                Extensions = new List<string> { ".toml" },
                Syntax = new SyntaxProfile
                {
                    Keywords = new List<string> { "true", "false" },
                    LineComments = new List<string> { "#" },
                    BlockComment = null,
                },
                Font = new FontProfile { Family = "Consolas", Size = 11f },
                Format = new FormatProfile { IndentSize = 2, ReindentByBrackets = true, EnsureFinalNewline = true },
                Brackets = new List<List<string>> { new() { "[", "]" }, new() { "{", "}" } },
                Diagnostics = new List<DiagnosticRule>
                {
                    new() { Pattern = @"\bTODO\b", Message = "Unresolved TODO.", Severity = "info" },
                    new() { Pattern = "\t", Message = "Tab character (TOML prefers spaces).", Severity = "warning" },
                },
                Completions = new List<string> { "version", "dependencies", "description", "authors" },
            },
        },
    };
}
