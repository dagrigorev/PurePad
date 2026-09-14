namespace PurePad.LargeFiles;

/// <summary>
/// Random-access, read-only character source for the immutable "original" side of a
/// <see cref="PieceTable"/>. For large files this is backed by the memory-mapped file, so
/// text is decoded on demand rather than held in managed memory.
/// </summary>
public interface IOriginalText
{
    /// <summary>Total number of characters (UTF-16 code units).</summary>
    int Length { get; }

    /// <summary>Return the characters in <c>[start, start + length)</c>.</summary>
    string GetText(int start, int length);
}
