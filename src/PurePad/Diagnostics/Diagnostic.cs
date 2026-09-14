namespace PurePad.Diagnostics;

/// <summary>Severity of a reported problem, mirroring a linter's levels.</summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// One problem found while checking a document: where it is (1-based line/column) and
/// what it is. Immutable and UI-free so checkers stay testable.
/// </summary>
public sealed class Diagnostic
{
    public Diagnostic(DiagnosticSeverity severity, int line, int column, string message)
    {
        Severity = severity;
        Line = Math.Max(1, line);
        Column = Math.Max(1, column);
        Message = message ?? string.Empty;
    }

    public DiagnosticSeverity Severity { get; }

    public int Line { get; }

    public int Column { get; }

    public string Message { get; }

    public override string ToString() => $"{Severity} (Ln {Line}, Col {Column}): {Message}";
}
