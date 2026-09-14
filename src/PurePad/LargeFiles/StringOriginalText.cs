namespace PurePad.LargeFiles;

/// <summary>An in-memory <see cref="IOriginalText"/> over a string (used by tests and small files).</summary>
public sealed class StringOriginalText : IOriginalText
{
    private readonly string _text;

    public StringOriginalText(string text) => _text = text ?? string.Empty;

    public int Length => _text.Length;

    public string GetText(int start, int length) => _text.Substring(start, length);
}
