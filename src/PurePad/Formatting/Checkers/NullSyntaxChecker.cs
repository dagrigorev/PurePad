using PurePad.Diagnostics;

namespace PurePad.Formatting.Checkers;

/// <summary>Null Object checker: never reports a problem. Used for languages with no built-in validation.</summary>
public sealed class NullSyntaxChecker : ISyntaxChecker
{
    public static readonly NullSyntaxChecker Instance = new();

    private NullSyntaxChecker()
    {
    }

    public bool CanCheck => false;

    public IReadOnlyList<Diagnostic> Check(string text) => Array.Empty<Diagnostic>();
}
