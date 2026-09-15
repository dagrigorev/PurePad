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

    /// <summary>
    /// JavaScript (and JSX) reserved words plus a handful of ubiquitous globals, so a dedicated
    /// language definition colours JS more precisely than the broad C-family union.
    /// </summary>
    public static readonly string[] JavaScript =
    {
        // Declarations
        "var", "let", "const", "function", "class", "extends", "static", "get", "set",
        "import", "export", "from", "as", "default", "async", "await", "yield",
        // Control flow
        "if", "else", "for", "while", "do", "switch", "case", "break", "continue",
        "return", "try", "catch", "finally", "throw", "with", "debugger",
        // Operators & values
        "new", "delete", "typeof", "instanceof", "void", "in", "of", "this", "super",
        "true", "false", "null", "undefined", "NaN", "Infinity",
        // Common globals
        "console", "window", "document", "globalThis", "arguments", "Promise", "Math", "JSON",
    };

    /// <summary>Bash / shell reserved words and common builtins.</summary>
    public static readonly string[] Bash =
    {
        "if", "then", "else", "elif", "fi", "for", "while", "until", "do", "done", "case", "esac",
        "function", "in", "select", "return", "break", "continue", "time", "coproc",
        "local", "declare", "typeset", "readonly", "export", "unset", "shift", "eval", "exec",
        "source", "alias", "trap", "set", "let", "test",
        "echo", "printf", "read", "cd", "pwd", "exit", "kill", "wait",
        "true", "false",
    };

    /// <summary>PowerShell keywords (matched case-insensitively).</summary>
    public static readonly string[] PowerShell =
    {
        "function", "filter", "workflow", "configuration", "param", "begin", "process", "end",
        "dynamicparam", "if", "elseif", "else", "switch", "foreach", "for", "while", "do", "until",
        "return", "break", "continue", "try", "catch", "finally", "throw", "trap", "exit",
        "class", "enum", "using", "namespace", "module", "in", "default", "hidden", "static",
        "parallel", "sequence", "data", "inlinescript",
    };

    /// <summary>Windows batch (cmd) keywords and common commands (matched case-insensitively).</summary>
    public static readonly string[] Cmd =
    {
        "echo", "set", "setlocal", "endlocal", "if", "else", "for", "in", "do", "goto", "call",
        "exit", "shift", "pause", "rem", "cls", "start", "choice", "errorlevel", "exist", "not",
        "defined", "cd", "chdir", "md", "mkdir", "rd", "rmdir", "del", "erase", "copy", "xcopy",
        "move", "ren", "rename", "type", "pushd", "popd", "title", "prompt", "nul", "equ", "neq",
        "lss", "leq", "gtr", "geq",
    };

    /// <summary>SQL keywords, clauses, operators, common functions and types (matched case-insensitively).</summary>
    public static readonly string[] Sql =
    {
        // Statements
        "select", "insert", "update", "delete", "merge", "truncate", "create", "alter", "drop",
        "grant", "revoke", "begin", "commit", "rollback", "savepoint", "declare", "set", "use",
        "explain", "with", "call", "execute", "exec", "return", "if", "else", "while", "loop",
        // Clauses
        "from", "where", "group", "by", "having", "order", "limit", "offset", "fetch", "next",
        "rows", "only", "top", "distinct", "all", "into", "values", "as", "on", "using",
        "union", "intersect", "except", "over", "partition", "window", "returning", "output",
        // Joins
        "join", "inner", "left", "right", "full", "outer", "cross", "natural", "lateral", "apply",
        // Operators / predicates
        "and", "or", "not", "in", "exists", "between", "like", "ilike", "is", "null", "case",
        "when", "then", "end", "any", "some", "asc", "desc", "escape", "collate",
        // DDL / constraints
        "table", "view", "index", "sequence", "trigger", "procedure", "function", "database",
        "schema", "column", "constraint", "primary", "foreign", "key", "references", "unique",
        "check", "default", "identity", "auto_increment", "cascade", "restrict", "add", "rename",
        // Types
        "int", "integer", "smallint", "bigint", "tinyint", "decimal", "numeric", "float", "real",
        "double", "precision", "bit", "boolean", "bool", "char", "varchar", "nvarchar", "text",
        "nchar", "ntext", "date", "time", "datetime", "timestamp", "interval", "money", "uuid",
        "json", "jsonb", "blob", "clob", "binary", "varbinary", "serial",
        // Common functions
        "count", "sum", "avg", "min", "max", "coalesce", "nullif", "cast", "convert", "trim",
        "upper", "lower", "substring", "length", "replace", "concat", "now", "current_timestamp",
        "current_date", "current_user", "row_number", "rank", "dense_rank",
    };
}
