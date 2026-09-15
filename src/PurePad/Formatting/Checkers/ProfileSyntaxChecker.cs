using System.Text.RegularExpressions;
using PurePad.Diagnostics;
using PurePad.Formatting.Profiles;

namespace PurePad.Formatting.Checkers;

/// <summary>
/// A checker driven by a user's language profile: it flags bracket-balance problems and matches of
/// user-authored regex rules, all surfaced in the Problems panel. It is a lightweight linter (no
/// grammar), so it catches shape errors — unmatched brackets, forbidden patterns — rather than full
/// semantic errors.
/// </summary>
public sealed class ProfileSyntaxChecker : ISyntaxChecker
{
    private const int MaxDiagnostics = 500;

    private readonly IReadOnlyDictionary<char, char> _open;   // open -> close
    private readonly IReadOnlyDictionary<char, char> _close;  // close -> open
    private readonly IReadOnlyList<CompiledRule> _rules;

    private ProfileSyntaxChecker(
        IReadOnlyDictionary<char, char> open,
        IReadOnlyDictionary<char, char> close,
        IReadOnlyList<CompiledRule> rules)
    {
        _open = open;
        _close = close;
        _rules = rules;
    }

    private sealed record CompiledRule(Regex Regex, string Message, DiagnosticSeverity Severity);

    public bool CanCheck => _open.Count > 0 || _rules.Count > 0;

    /// <summary>Build a checker from a profile, or the no-op checker when it defines no rules.</summary>
    public static ISyntaxChecker Create(LanguageProfile profile)
    {
        var open = new Dictionary<char, char>();
        var close = new Dictionary<char, char>();
        foreach (List<string> pair in profile.Brackets)
        {
            if (pair.Count == 2 && pair[0].Length == 1 && pair[1].Length == 1)
            {
                open[pair[0][0]] = pair[1][0];
                close[pair[1][0]] = pair[0][0];
            }
        }

        var rules = new List<CompiledRule>();
        foreach (DiagnosticRule rule in profile.Diagnostics)
        {
            if (string.IsNullOrEmpty(rule.Pattern))
            {
                continue;
            }

            try
            {
                var options = RegexOptions.CultureInvariant | (rule.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
                var regex = new Regex(rule.Pattern, options, TimeSpan.FromMilliseconds(100));
                rules.Add(new CompiledRule(regex, rule.Message, ParseSeverity(rule.Severity)));
            }
            catch (ArgumentException)
            {
                // Skip an invalid regex rather than failing the whole checker.
            }
        }

        return open.Count == 0 && rules.Count == 0
            ? NullSyntaxChecker.Instance
            : new ProfileSyntaxChecker(open, close, rules);
    }

    public IReadOnlyList<Diagnostic> Check(string text)
    {
        var diagnostics = new List<Diagnostic>();
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        RunRegexRules(lines, diagnostics);
        if (diagnostics.Count < MaxDiagnostics)
        {
            CheckBrackets(lines, diagnostics);
        }

        return diagnostics;
    }

    private void RunRegexRules(string[] lines, List<Diagnostic> diagnostics)
    {
        for (int i = 0; i < lines.Length && diagnostics.Count < MaxDiagnostics; i++)
        {
            foreach (CompiledRule rule in _rules)
            {
                try
                {
                    foreach (Match m in rule.Regex.Matches(lines[i]))
                    {
                        diagnostics.Add(new Diagnostic(rule.Severity, i + 1, m.Index + 1, rule.Message));
                        if (diagnostics.Count >= MaxDiagnostics)
                        {
                            return;
                        }
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // Pathological pattern/line — skip this line for this rule.
                }
            }
        }
    }

    /// <summary>Balance the profile's bracket pairs, skipping strings and comments.</summary>
    private void CheckBrackets(string[] lines, List<Diagnostic> diagnostics)
    {
        var stack = new Stack<(char Ch, int Line, int Col)>();

        for (int li = 0; li < lines.Length; li++)
        {
            string line = lines[li];
            for (int ci = 0; ci < line.Length; ci++)
            {
                char c = line[ci];

                if (c == '/' && ci + 1 < line.Length && line[ci + 1] == '/')
                {
                    break; // rest of line is a comment
                }

                if (c is '"' or '\'' or '`')
                {
                    ci = SkipString(line, ci, c);
                    continue;
                }

                if (_open.ContainsKey(c))
                {
                    stack.Push((c, li + 1, ci + 1));
                }
                else if (_close.TryGetValue(c, out char expectedOpen))
                {
                    if (stack.Count == 0)
                    {
                        diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, li + 1, ci + 1, $"Unmatched '{c}'."));
                    }
                    else if (stack.Peek().Ch != expectedOpen)
                    {
                        (char ch, int line2, int col) = stack.Pop();
                        diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, line2, col, $"Mismatched '{ch}' — closed by '{c}'."));
                    }
                    else
                    {
                        stack.Pop();
                    }

                    if (diagnostics.Count >= MaxDiagnostics)
                    {
                        return;
                    }
                }
            }
        }

        foreach ((char ch, int line, int col) in stack)
        {
            diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, line, col, $"Unclosed '{ch}'."));
            if (diagnostics.Count >= MaxDiagnostics)
            {
                return;
            }
        }
    }

    private static int SkipString(string line, int start, char quote)
    {
        int i = start + 1;
        while (i < line.Length)
        {
            if (line[i] == '\\') { i += 2; continue; }
            if (line[i] == quote) { return i; }
            i++;
        }

        return line.Length - 1; // unterminated on this line — stop here
    }

    private static DiagnosticSeverity ParseSeverity(string severity) => severity.Trim().ToLowerInvariant() switch
    {
        "error" => DiagnosticSeverity.Error,
        "info" => DiagnosticSeverity.Info,
        _ => DiagnosticSeverity.Warning,
    };
}
