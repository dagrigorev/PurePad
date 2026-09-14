using System.Text.Json;
using PurePad.Diagnostics;

namespace PurePad.Formatting.Checkers;

/// <summary>
/// Validates JSON with <see cref="System.Text.Json"/>, turning a parse failure into a
/// single located <see cref="Diagnostic"/>. Empty input is treated as clean.
/// </summary>
public sealed class JsonSyntaxChecker : ISyntaxChecker
{
    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public bool CanCheck => true;

    public IReadOnlyList<Diagnostic> Check(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<Diagnostic>();
        }

        try
        {
            using JsonDocument _ = JsonDocument.Parse(text, Options);
            return Array.Empty<Diagnostic>();
        }
        catch (JsonException ex)
        {
            int line = (int)(ex.LineNumber ?? 0) + 1;
            int column = (int)(ex.BytePositionInLine ?? 0) + 1;
            return new[] { new Diagnostic(DiagnosticSeverity.Error, line, column, ex.Message) };
        }
    }
}
