using System.Text;
using System.Text.RegularExpressions;
using PurePad.Formatting.Highlighters;

namespace PurePad.Editor;

/// <summary>What a symbol is, so hovers/navigation can label it.</summary>
public enum SymbolKind
{
    Macro,
    Type,
    Function,
    Variable,
    Include,
}

/// <summary>A named definition found in a document: where it is and its signature/doc text.</summary>
public sealed record SymbolDefinition(string Name, int Line, SymbolKind Kind, string Signature, string Doc);

/// <summary>
/// A lightweight, language-agnostic index of a source document, built by scanning lines with
/// simple heuristics (no full grammar). It powers three editor features: identifier completion,
/// Ctrl+click go-to-definition, and hover documentation. It recognises C-family / C# shapes —
/// <c>#define</c>, <c>#include</c>, type declarations, functions and simple variables — and
/// attaches the doc comment that precedes (or trails) a definition.
/// </summary>
public sealed class DocumentIndex
{
    private static readonly Regex Identifier = new(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);
    private static readonly HashSet<string> Keywords = new(CodeKeywords.CFamily, StringComparer.Ordinal);

    // Keywords that can precede "name(" without it being a definition (control flow / calls).
    private static readonly HashSet<string> NonTypeLeaders = new(StringComparer.Ordinal)
    {
        "if", "for", "foreach", "while", "switch", "catch", "return", "sizeof", "typeof", "do", "else",
    };

    private readonly SortedSet<string> _identifiers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SymbolDefinition> _definitions = new(StringComparer.Ordinal);

    private DocumentIndex() { }

    public IReadOnlyCollection<string> Identifiers => _identifiers;

    public bool TryGetDefinition(string name, out SymbolDefinition definition) =>
        _definitions.TryGetValue(name, out definition!);

    /// <summary>
    /// Document identifiers plus <paramref name="extraWords"/> (the language's keywords/completions)
    /// that start with <paramref name="prefix"/>, ranked.
    /// </summary>
    public IReadOnlyList<string> CompletionsFor(string prefix, IEnumerable<string> extraWords, int max = 12)
    {
        if (prefix.Length == 0)
        {
            return Array.Empty<string>();
        }

        return _identifiers.Concat(extraWords)
            .Where(w => w.Length > prefix.Length && w.StartsWith(prefix, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(w => w, StringComparer.Ordinal)
            .Take(max)
            .ToList();
    }

    /// <summary>
    /// If <paramref name="column"/> sits inside an <c>#include "path"</c> / <c>&lt;path&gt;</c>
    /// clause on <paramref name="lineText"/>, return the include path; otherwise null.
    /// </summary>
    public static string? IncludeTargetAt(string lineText, int column)
    {
        Match m = Regex.Match(lineText, "#\\s*include\\s*[\"<]([^\">]+)[\">]");
        if (!m.Success)
        {
            return null;
        }

        Group path = m.Groups[1];
        return column >= m.Index && column <= path.Index + path.Length ? path.Value : null;
    }

    /// <summary>Extract the identifier that spans <paramref name="column"/> in <paramref name="lineText"/>.</summary>
    public static string WordAt(string lineText, int column)
    {
        foreach (Match m in Identifier.Matches(lineText))
        {
            if (column >= m.Index && column <= m.Index + m.Length)
            {
                return m.Value;
            }
        }

        return string.Empty;
    }

    /// <summary>Parse <paramref name="text"/> into an index.</summary>
    public static DocumentIndex Build(string text)
    {
        var index = new DocumentIndex();
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var pendingDoc = new List<string>();
        bool inBlockComment = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            string line = raw.TrimEnd();
            string trimmed = line.TrimStart();

            // Collect every identifier for completion.
            foreach (Match m in Identifier.Matches(raw))
            {
                index._identifiers.Add(m.Value);
            }

            // Track doc-comment blocks that precede a definition.
            if (inBlockComment)
            {
                pendingDoc.Add(trimmed);
                if (trimmed.Contains("*/"))
                {
                    inBlockComment = false;
                }

                continue;
            }

            if (trimmed.StartsWith("///") || trimmed.StartsWith("//!") || trimmed.StartsWith("/**") || trimmed.StartsWith("/*!"))
            {
                pendingDoc.Add(trimmed);
                inBlockComment = trimmed.StartsWith("/*") && !trimmed.Contains("*/");
                continue;
            }

            if (trimmed.Length == 0)
            {
                pendingDoc.Clear(); // a blank line breaks the association between comment and code
                continue;
            }

            string doc = BuildDoc(pendingDoc, line);
            index.Detect(trimmed, line, i, doc);
            pendingDoc.Clear();
        }

        return index;
    }

    /// <summary>Detect a definition on a code line and record it (first definition wins).</summary>
    private void Detect(string trimmed, string line, int lineIndex, string doc)
    {
        // #define NAME
        Match def = Regex.Match(trimmed, @"^#\s*define\s+([A-Za-z_]\w*)");
        if (def.Success)
        {
            Add(def.Groups[1].Value, lineIndex, SymbolKind.Macro, line.Trim(), doc);
            return;
        }

        // #include "path" / <path>
        Match inc = Regex.Match(trimmed, "^#\\s*include\\s*[\"<]([^\">]+)[\">]");
        if (inc.Success)
        {
            Add(inc.Groups[1].Value, lineIndex, SymbolKind.Include, line.Trim(), doc);
            return;
        }

        // struct/class/enum/union/namespace/interface/record NAME
        Match type = Regex.Match(trimmed, @"^(?:typedef\s+)?(?:struct|class|enum|union|namespace|interface|record)\b[^\w]*([A-Za-z_]\w*)");
        if (type.Success)
        {
            Add(type.Groups[1].Value, lineIndex, SymbolKind.Type, line.Trim(), doc);
            return;
        }

        // Function/method: leader NAME( where leader is a type-ish word (not a control keyword).
        foreach (Match fn in Regex.Matches(trimmed, @"([A-Za-z_]\w*)\s+([A-Za-z_]\w*)\s*\("))
        {
            string leader = fn.Groups[1].Value;
            string name = fn.Groups[2].Value;
            if (!NonTypeLeaders.Contains(leader) && !NonTypeLeaders.Contains(name))
            {
                Add(name, lineIndex, SymbolKind.Function, line.Trim(), doc);
                return;
            }
        }

        // Simple variable/field: leader NAME = ...  or  leader NAME ;
        Match var = Regex.Match(trimmed, @"^([A-Za-z_][\w:<>\*\&\s]*?\s[\*\&]?)([A-Za-z_]\w*)\s*(?:=|;)");
        if (var.Success)
        {
            string name = var.Groups[2].Value;
            if (!Keywords.Contains(name))
            {
                Add(name, lineIndex, SymbolKind.Variable, line.Trim(), doc);
            }
        }
    }

    private void Add(string name, int line, SymbolKind kind, string signature, string doc)
    {
        if (name.Length == 0 || Keywords.Contains(name))
        {
            return;
        }

        _definitions.TryAdd(name, new SymbolDefinition(name, line, kind, signature, doc));
    }

    /// <summary>Assemble hover text from the pending doc block and any trailing line comment.</summary>
    private static string BuildDoc(List<string> pendingDoc, string codeLine)
    {
        var sb = new StringBuilder();
        foreach (string d in pendingDoc)
        {
            string clean = d.TrimStart('/', '!', '*', ' ', '\t').TrimEnd('*', '/', ' ', '\t');
            if (clean.Length > 0)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(clean);
            }
        }

        // Trailing // comment on the definition line itself.
        int slash = codeLine.IndexOf("//", StringComparison.Ordinal);
        if (slash >= 0)
        {
            string trailing = codeLine[(slash + 2)..].Trim();
            if (trailing.Length > 0)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(trailing);
            }
        }

        return sb.ToString();
    }
}
