using PurePad.Diagnostics;

namespace PurePad.Formatting;

/// <summary>
/// Strategy that inspects document text and reports problems. Independent from
/// highlighting and formatting (Interface Segregation): a language can colour without
/// checking, or check without reformatting.
/// </summary>
public interface ISyntaxChecker
{
    /// <summary>Whether this checker performs any real validation (false for the no-op).</summary>
    bool CanCheck { get; }

    /// <summary>Return all problems found in <paramref name="text"/>; empty when clean.</summary>
    IReadOnlyList<Diagnostic> Check(string text);
}
