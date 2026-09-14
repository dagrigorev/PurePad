using System.Text;

namespace PurePad.Services;

/// <summary>
/// Abstraction over reading and writing text files. Depending on this rather than
/// <see cref="File"/> directly keeps the editor logic decoupled from the filesystem
/// (Dependency Inversion) and testable with an in-memory fake.
/// </summary>
public interface IFileService
{
    /// <summary>Read a text file, detecting its encoding from any byte-order mark.</summary>
    /// <exception cref="IOException">On read failure.</exception>
    FileContent Read(string path);

    /// <summary>
    /// Read a text file asynchronously, decoding it in chunks off the calling thread and
    /// reporting fractional progress (0..1). Keeps the UI responsive for large files.
    /// </summary>
    /// <exception cref="IOException">On read failure.</exception>
    /// <exception cref="OperationCanceledException">If cancelled.</exception>
    Task<FileContent> ReadAsync(string path, IProgress<double>? progress, CancellationToken cancellationToken);

    /// <summary>Write <paramref name="text"/> to <paramref name="path"/> using <paramref name="encoding"/>.</summary>
    /// <exception cref="IOException">On write failure.</exception>
    void Write(string path, string text, Encoding encoding);
}
