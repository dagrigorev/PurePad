using System.Text;

namespace PurePad.Services;

/// <summary>Immutable result of reading a text file: its content and detected encoding.</summary>
public sealed class FileContent
{
    public FileContent(string text, Encoding encoding, bool truncated = false)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        Truncated = truncated;
    }

    public string Text { get; }

    public Encoding Encoding { get; }

    /// <summary>True when the file was larger than the load cap and only a prefix was read.</summary>
    public bool Truncated { get; }
}
