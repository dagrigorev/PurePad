namespace PurePad.Formatting.Highlighters;

/// <summary>
/// Keyword vocabularies for <see cref="CodeSyntaxHighlighter"/>. Kept as data (not code)
/// so a single generic scanner can serve many C-family languages.
/// </summary>
public static class CodeKeywords
{
    /// <summary>
    /// A union of common reserved words across C#, Java, JavaScript/TypeScript, C/C++ and
    /// a few scripting languages. A shared set gives useful colouring without a bespoke
    /// grammar per language.
    /// </summary>
    public static readonly string[] CFamily =
    {
        // Declarations & types
        "class", "struct", "enum", "interface", "namespace", "using", "import", "package",
        "public", "private", "protected", "internal", "static", "readonly", "const", "final",
        "abstract", "sealed", "virtual", "override", "partial", "extern", "async", "await",
        "var", "let", "function", "def", "void", "int", "long", "short", "byte", "bool",
        "boolean", "char", "float", "double", "decimal", "string", "object", "auto", "unsigned",
        "signed", "typedef", "template", "typename", "record",
        // Control flow
        "if", "else", "for", "foreach", "while", "do", "switch", "case", "default", "break",
        "continue", "return", "goto", "yield", "throw", "try", "catch", "finally", "elif",
        // Values & operators
        "true", "false", "null", "nil", "none", "new", "delete", "this", "self", "super",
        "base", "typeof", "sizeof", "instanceof", "is", "as", "in", "out", "ref", "and", "or", "not",
    };
}
