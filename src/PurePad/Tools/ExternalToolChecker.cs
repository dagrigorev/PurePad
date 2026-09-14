using System.Text.RegularExpressions;
using PurePad.Diagnostics;
using PurePad.Formatting;

namespace PurePad.Tools;

/// <summary>
/// An <see cref="ISyntaxChecker"/> backed by an external linter. The document is piped to
/// stdin; the tool's output lines are parsed into <see cref="Diagnostic"/> objects with a
/// configurable regex (named groups <c>line</c>, <c>col</c>, <c>severity</c>, <c>message</c>).
/// Any launch failure is surfaced as a single warning rather than crashing the editor.
/// </summary>
public sealed class ExternalToolChecker : ISyntaxChecker
{
    private const string DefaultPattern = @"(?<line>\d+)[:,]\s*(?:col(?:umn)?\s*)?(?<col>\d+)?[:,]?\s*(?<message>.+)";

    private readonly ExternalToolRunner _runner;
    private readonly ToolCommand _command;
    private readonly Regex _pattern;

    public ExternalToolChecker(ExternalToolRunner runner, ToolCommand command)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _pattern = new Regex(
            string.IsNullOrWhiteSpace(command.Pattern) ? DefaultPattern : command.Pattern,
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    public bool CanCheck => _command.IsValid;

    public IReadOnlyList<Diagnostic> Check(string text)
    {
        ToolResult result;
        try
        {
            result = _runner.Run(_command.Command, _command.Args, text);
        }
        catch (ToolExecutionException ex)
        {
            return new[] { new Diagnostic(DiagnosticSeverity.Warning, 1, 1, ex.Message) };
        }

        if (result.Succeeded && string.IsNullOrWhiteSpace(result.StandardError))
        {
            return Array.Empty<Diagnostic>();
        }

        string combined = result.StandardOutput + "\n" + result.StandardError;
        var diagnostics = new List<Diagnostic>();

        foreach (string line in combined.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            Match match = _pattern.Match(trimmed);
            if (!match.Success)
            {
                continue;
            }

            diagnostics.Add(new Diagnostic(
                ParseSeverity(match.Groups["severity"].Value),
                ParseInt(match.Groups["line"].Value, 1),
                ParseInt(match.Groups["col"].Value, 1),
                match.Groups["message"].Success ? match.Groups["message"].Value.Trim() : trimmed));
        }

        // Tool failed but produced nothing we could parse: still tell the user something is wrong.
        if (diagnostics.Count == 0 && !result.Succeeded)
        {
            diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, 1, 1, $"'{_command.Command}' reported problems (exit code {result.ExitCode})."));
        }

        return diagnostics;
    }

    private static DiagnosticSeverity ParseSeverity(string value) => value.ToLowerInvariant() switch
    {
        "error" or "err" or "e" => DiagnosticSeverity.Error,
        "warning" or "warn" or "w" => DiagnosticSeverity.Warning,
        "info" or "information" or "note" => DiagnosticSeverity.Info,
        _ => DiagnosticSeverity.Error,
    };

    private static int ParseInt(string value, int fallback) =>
        int.TryParse(value, out int parsed) ? parsed : fallback;
}
