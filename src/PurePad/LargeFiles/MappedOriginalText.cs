namespace PurePad.LargeFiles;

/// <summary>
/// Exposes a memory-mapped <see cref="LargeFileDocument"/> as an <see cref="IOriginalText"/>
/// so it can back an editable <see cref="PieceTable"/> without loading the file into memory.
/// </summary>
public sealed class MappedOriginalText : IOriginalText
{
    private readonly LargeFileDocument _document;

    public MappedOriginalText(LargeFileDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public int Length => (int)Math.Max(0, _document.CharLength);

    public string GetText(int start, int length) => _document.GetTextByChars(start, length);
}
