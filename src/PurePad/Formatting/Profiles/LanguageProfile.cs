namespace PurePad.Formatting.Profiles;

/// <summary>
/// A user-editable description of how one file type is coloured, formatted and displayed. Loaded
/// from <c>languages.json</c>; users may edit a section or add a new one to teach the editor a new
/// file type. Everything is optional beyond <see cref="Id"/>/<see cref="Extensions"/> — omitted
/// fields fall back to sensible defaults.
/// </summary>
public sealed class LanguageProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Extensions { get; set; } = new();

    public SyntaxProfile Syntax { get; set; } = new();

    /// <summary>Per-token colour overrides, keyed by <see cref="TokenKind"/> name → hex (e.g. "#569CD6").</summary>
    public Dictionary<string, string> Colors { get; set; } = new();

    public FontProfile? Font { get; set; }

    public FormatProfile Format { get; set; } = new();

    /// <summary>Bracket pairs, e.g. <c>[["{","}"],["(",")"]]</c>, checked for balance.</summary>
    public List<List<string>> Brackets { get; set; } = new();

    /// <summary>Regex-based problem rules surfaced in the Problems panel.</summary>
    public List<DiagnosticRule> Diagnostics { get; set; } = new();

    /// <summary>Extra words offered as completion suggestions (beyond keywords/identifiers).</summary>
    public List<string> Completions { get; set; } = new();

    /// <summary>Optional Language Server Protocol integration for this file type.</summary>
    public LspProfile? Lsp { get; set; }
}

/// <summary>Configures a Language Server for a file type (the server binary must be on PATH).</summary>
public sealed class LspProfile
{
    public bool Enabled { get; set; } = true;

    /// <summary>The server executable (looked up on PATH), e.g. "pyright-langserver".</summary>
    public string Command { get; set; } = "";

    public List<string> Args { get; set; } = new();

    /// <summary>The LSP language id sent to the server, e.g. "python".</summary>
    public string LanguageId { get; set; } = "";

    /// <summary>Files/dirs that mark the workspace root (searched upward from the file).</summary>
    public List<string> RootMarkers { get; set; } = new() { ".git" };
}

/// <summary>A user-authored linting rule: a regex whose matches become problems.</summary>
public sealed class DiagnosticRule
{
    public string Pattern { get; set; } = "";

    public string Message { get; set; } = "";

    /// <summary>"error", "warning" or "info" (default warning).</summary>
    public string Severity { get; set; } = "warning";

    public bool IgnoreCase { get; set; }
}

/// <summary>Tokenizer configuration for a profile (mirrors the code highlighter's options).</summary>
public sealed class SyntaxProfile
{
    public List<string> Keywords { get; set; } = new();
    public List<string> LineComments { get; set; } = new() { "//" };
    public BlockCommentProfile? BlockComment { get; set; } = new() { Open = "/*", Close = "*/" };

    /// <summary>Variable sigil to colour: "$" or "%", else empty.</summary>
    public string VariableSigil { get; set; } = "";

    public bool RegexLiterals { get; set; }
    public bool TemplateStrings { get; set; }
    public bool CaseInsensitiveKeywords { get; set; }
    public bool RemLineComments { get; set; }
}

public sealed class BlockCommentProfile
{
    public string Open { get; set; } = "/*";
    public string Close { get; set; } = "*/";
}

public sealed class FontProfile
{
    public string Family { get; set; } = "Consolas";
    public float Size { get; set; } = 10.5f;

    /// <summary>Optional style: "Regular", "Bold", "Italic" (combinable with "+").</summary>
    public string Style { get; set; } = "Regular";
}

/// <summary>Rules for the built-in beautifier (Format command / format-on-save).</summary>
public sealed class FormatProfile
{
    public int IndentSize { get; set; } = 4;
    public bool UseTabs { get; set; }
    public bool ReindentByBrackets { get; set; } = true;
    public bool TrimTrailingWhitespace { get; set; } = true;
    public bool EnsureFinalNewline { get; set; }
}
