using System.Text;

namespace PurePad.Services;

/// <summary>
/// Filesystem-backed <see cref="IFileService"/>. On read it inspects the byte-order mark
/// to preserve the file's original encoding on the next save (matching Notepad's
/// behaviour); a file without a BOM is treated as UTF-8 without a BOM.
/// </summary>
public sealed class FileService : IFileService
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public FileContent Read(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Encoding encoding = DetectEncoding(bytes, out int preambleLength);
        string text = encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);
        return new FileContent(text, encoding);
    }

    public void Write(string path, string text, Encoding encoding)
    {
        File.WriteAllText(path, text, encoding);
    }

    public async Task<FileContent> ReadAsync(string path, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        const int decodeBufferChars = 1 << 16; // 64K chars per chunk
        const long reportEveryBytes = 1 << 20; // report at most ~every 1 MB

        // Hard ceiling: the rich-edit control (and a single in-memory string) cannot host a
        // multi-hundred-MB document without freezing or running out of memory. Above this we
        // load a prefix and mark it truncated so the app stays alive and responsive.
        const int maxLoadChars = 40_000_000; // ~80 MB of UTF-16

        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1 << 20, FileOptions.Asynchronous | FileOptions.SequentialScan);

        long length = stream.Length;
        Encoding encoding = await DetectEncodingAsync(stream, cancellationToken).ConfigureAwait(false);

        // Encoding already chosen (and any BOM skipped); do not let the reader re-sniff.
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false, decodeBufferChars, leaveOpen: true);

        int initialCapacity = length > 0 && length < maxLoadChars ? (int)length : maxLoadChars;
        var builder = new StringBuilder(initialCapacity);
        char[] buffer = new char[decodeBufferChars];
        long lastReported = 0;
        bool truncated = false;
        int read;

        while ((read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            int remaining = maxLoadChars - builder.Length;
            if (read >= remaining)
            {
                builder.Append(buffer, 0, remaining);
                truncated = true;
                break; // reached the load cap; stop reading the rest of the file
            }

            builder.Append(buffer, 0, read);

            if (progress is not null && length > 0 && stream.Position - lastReported >= reportEveryBytes)
            {
                lastReported = stream.Position;
                progress.Report(Math.Min(1.0, (double)stream.Position / length));
            }
        }

        progress?.Report(1.0);
        return new FileContent(builder.ToString(), encoding, truncated);
    }

    /// <summary>Peek the leading bytes for a BOM, position the stream past it, and return the encoding.</summary>
    private static async Task<Encoding> DetectEncodingAsync(FileStream stream, CancellationToken cancellationToken)
    {
        byte[] head = new byte[4];
        int got = await stream.ReadAsync(head.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);

        Encoding encoding = DetectEncoding(head.AsSpan(0, got).ToArray(), out int preambleLength);
        stream.Position = preambleLength; // rewind past any BOM (or to 0 when none)
        return encoding;
    }

    /// <summary>Choose an encoding from the leading BOM, defaulting to UTF-8 without BOM.</summary>
    private static Encoding DetectEncoding(byte[] bytes, out int preambleLength)
    {
        if (StartsWith(bytes, 0xEF, 0xBB, 0xBF))
        {
            preambleLength = 3;
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }

        if (StartsWith(bytes, 0xFF, 0xFE))
        {
            preambleLength = 2;
            return new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        }

        if (StartsWith(bytes, 0xFE, 0xFF))
        {
            preambleLength = 2;
            return new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
        }

        preambleLength = 0;
        return Utf8NoBom;
    }

    private static bool StartsWith(byte[] bytes, params byte[] prefix)
    {
        if (bytes.Length < prefix.Length)
        {
            return false;
        }

        for (int i = 0; i < prefix.Length; i++)
        {
            if (bytes[i] != prefix[i])
            {
                return false;
            }
        }

        return true;
    }
}
