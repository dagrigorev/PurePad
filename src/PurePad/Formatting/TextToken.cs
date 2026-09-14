namespace PurePad.Formatting;

/// <summary>
/// An immutable, coloured span of the document: a half-open range
/// <c>[Start, Start + Length)</c> tagged with a <see cref="TokenKind"/>.
/// Highlighters emit these; the renderer turns them into colour runs. The type is
/// deliberately free of any UI dependency so highlighters stay unit-testable.
/// </summary>
public readonly struct TextToken
{
    public TextToken(int start, int length, TokenKind kind)
    {
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

        Start = start;
        Length = length;
        Kind = kind;
    }

    /// <summary>Zero-based index of the first character in the span.</summary>
    public int Start { get; }

    /// <summary>Number of characters covered by the span.</summary>
    public int Length { get; }

    /// <summary>Index just past the last character in the span.</summary>
    public int End => Start + Length;

    public TokenKind Kind { get; }

    public override string ToString() => $"{Kind} [{Start}..{End})";
}
