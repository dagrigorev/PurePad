using System.IO.MemoryMappedFiles;
using System.Text;

namespace PurePad.LargeFiles;

/// <summary>
/// A read-only window onto a very large file. The file is memory-mapped (never fully read
/// into managed memory) and scanned once to index the byte offset of every line, so any
/// line can be fetched on demand. This is what lets PurePad open 250–500 MB+ files instantly
/// and with a tiny footprint — the editable RichTextBox path cannot.
/// </summary>
public sealed class LargeFileDocument : IDisposable
{
    private readonly MemoryMappedFile _map;
    private readonly MemoryMappedViewAccessor _view;
    private readonly long[] _lineStarts;   // byte offset of each line
    private readonly long[] _charStarts;    // char offset of each line (empty when not char-indexed)
    private readonly long _length;
    private readonly long _charLength;
    private readonly int _preamble;
    private readonly bool _isAscii;         // every byte < 0x80: char offset == byte offset

    private LargeFileDocument(string path, MemoryMappedFile map, MemoryMappedViewAccessor view, long[] lineStarts, long[] charStarts, long length, long charLength, int preamble, bool isAscii, Encoding encoding)
    {
        Path = path;
        _map = map;
        _view = view;
        _lineStarts = lineStarts;
        _charStarts = charStarts;
        _length = length;
        _charLength = charLength;
        _preamble = preamble;
        _isAscii = isAscii;
        Encoding = encoding;
    }

    public string Path { get; }

    public Encoding Encoding { get; }

    public int LineCount => _lineStarts.Length;

    /// <summary>Total characters, when char-indexed; otherwise -1.</summary>
    public long CharLength => _charLength;

    /// <summary>
    /// True when the file can back an editable piece table: it is char-indexed (UTF-8/ASCII)
    /// and small enough that character offsets fit in an int.
    /// </summary>
    public bool EditingSupported => _charStarts.Length > 0 && _charLength >= 0 && _charLength <= int.MaxValue;

    /// <summary>Char offsets of lines 1..n-1 (line 0 starts at 0), for seeding a piece table's line index.</summary>
    public IReadOnlyList<int> CharLineStarts()
    {
        var result = new int[Math.Max(0, _charStarts.Length - 1)];
        for (int i = 1; i < _charStarts.Length; i++)
        {
            result[i - 1] = (int)_charStarts[i];
        }

        return result;
    }

    /// <summary>
    /// Open <paramref name="path"/>: detect its encoding, scan it once to build the line
    /// index (reporting progress), then memory-map it for on-demand line reads.
    /// </summary>
    public static async Task<LargeFileDocument> OpenAsync(string path, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        long length = new FileInfo(path).Length;
        (Encoding encoding, int preamble) = await DetectEncodingAsync(path, cancellationToken).ConfigureAwait(false);

        // Char indexing (for editing) is exact only where a newline is a single 0x0A byte and
        // char counting can be done at the byte level — i.e. UTF-8 / ASCII, not UTF-16.
        bool countChars = encoding is UTF8Encoding;
        (long[] lineStarts, long[] charStarts, long charLength, bool isAscii) =
            await ScanLineStartsAsync(path, length, preamble, countChars, progress, cancellationToken).ConfigureAwait(false);

        MemoryMappedFile map = MemoryMappedFile.CreateFromFile(
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read),
            mapName: null, capacity: 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
        MemoryMappedViewAccessor view = map.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);

        return new LargeFileDocument(path, map, view, lineStarts, charStarts, length, charLength, preamble, isAscii, encoding);
    }

    /// <summary>Fetch a single line's text (without its line break).</summary>
    public string GetLine(int index)
    {
        if (index < 0 || index >= _lineStarts.Length)
        {
            return string.Empty;
        }

        long start = _lineStarts[index];
        long end = index + 1 < _lineStarts.Length ? _lineStarts[index + 1] : _length;
        int count = (int)Math.Min(end - start, int.MaxValue);
        if (count <= 0)
        {
            return string.Empty;
        }

        byte[] buffer = new byte[count];
        _view.ReadArray(start, buffer, 0, count);

        // Trim the trailing CR/LF that ends this line.
        int trimmed = count;
        while (trimmed > 0 && (buffer[trimmed - 1] == (byte)'\n' || buffer[trimmed - 1] == (byte)'\r'))
        {
            trimmed--;
        }

        return Encoding.GetString(buffer, 0, trimmed);
    }

    /// <summary>Full text of a line including its trailing CR/LF (for exact char mapping).</summary>
    public string GetLineRaw(int index)
    {
        if (index < 0 || index >= _lineStarts.Length)
        {
            return string.Empty;
        }

        long start = _lineStarts[index];
        long end = index + 1 < _lineStarts.Length ? _lineStarts[index + 1] : _length;
        int count = (int)Math.Min(end - start, int.MaxValue);
        if (count <= 0)
        {
            return string.Empty;
        }

        byte[] buffer = new byte[count];
        _view.ReadArray(start, buffer, 0, count);
        return Encoding.GetString(buffer, 0, count);
    }

    /// <summary>Read <paramref name="count"/> characters starting at char offset <paramref name="charStart"/>.</summary>
    public string GetTextByChars(int charStart, int count)
    {
        if (_charStarts.Length == 0 || count <= 0 || charStart >= _charLength)
        {
            return string.Empty;
        }

        count = (int)Math.Min(count, _charLength - charStart);

        // ASCII fast path: char offset == byte offset, so read the exact byte slice directly
        // (O(count)) — essential for slicing into a 100 MB single line without scanning it.
        if (_isAscii)
        {
            byte[] bytes = new byte[count];
            _view.ReadArray(_preamble + charStart, bytes, 0, count);
            return Encoding.GetString(bytes, 0, count);
        }

        var builder = new StringBuilder(count);
        int line = CharLineFromChar(charStart);
        int lineOffset = charStart - (int)_charStarts[line];
        int remaining = count;

        while (remaining > 0 && line < _lineStarts.Length)
        {
            string raw = GetLineRaw(line);
            if (lineOffset < raw.Length)
            {
                int take = Math.Min(raw.Length - lineOffset, remaining);
                builder.Append(raw, lineOffset, take);
                remaining -= take;
            }

            lineOffset = 0;
            line++;
        }

        return builder.ToString();
    }

    /// <summary>Whether every byte is &lt; 0x80 (enables O(1) char slicing).</summary>
    public bool IsAscii => _isAscii;

    /// <summary>Char offset at which line <paramref name="index"/> begins.</summary>
    public int GetLineCharStart(int index) => (int)_charStarts[index];

    /// <summary>Character length of a line excluding its trailing CR/LF — computed without reading the line.</summary>
    public int LineContentLength(int index)
    {
        long charLen = (index + 1 < _charStarts.Length ? _charStarts[index + 1] : _charLength) - _charStarts[index];
        long byteEnd = index + 1 < _lineStarts.Length ? _lineStarts[index + 1] : _length;
        long byteStart = _lineStarts[index];

        int breakLen = 0;
        if (byteEnd > byteStart && _view.ReadByte(byteEnd - 1) == (byte)'\n')
        {
            breakLen = 1;
            if (byteEnd - 2 >= byteStart && _view.ReadByte(byteEnd - 2) == (byte)'\r')
            {
                breakLen = 2;
            }
        }

        return (int)Math.Max(0, charLen - breakLen);
    }

    /// <summary>Read up to <paramref name="count"/> characters of line <paramref name="index"/> starting at column <paramref name="startColumn"/>.</summary>
    public string GetLineSlice(int index, int startColumn, int count)
    {
        int contentLength = LineContentLength(index);
        if (startColumn >= contentLength || count <= 0)
        {
            return string.Empty;
        }

        int take = Math.Min(count, contentLength - startColumn);
        return GetTextByChars(GetLineCharStart(index) + startColumn, take);
    }

    /// <summary>0-based line containing char offset <paramref name="charOffset"/>.</summary>
    public int LineFromChar(int charOffset) => CharLineFromChar(charOffset);

    private int CharLineFromChar(int charOffset)
    {
        int lo = 0, hi = _charStarts.Length - 1, result = 0;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (_charStarts[mid] <= charOffset)
            {
                result = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return result;
    }

    private static async Task<(long[] ByteStarts, long[] CharStarts, long CharLength, bool IsAscii)> ScanLineStartsAsync(
        string path, long length, int preamble, bool countChars, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var byteStarts = new List<long> { preamble };
        var charStarts = countChars ? new List<long> { 0 } : new List<long>();
        bool isAscii = true;

        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.Asynchronous | FileOptions.SequentialScan)
        {
            Position = preamble,
        };

        byte[] buffer = new byte[1 << 20];
        long position = preamble;
        long lastReported = preamble;
        long charCount = 0; // UTF-16 units, counting UTF-8 lead bytes (BMP assumption)
        int read;

        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                byte b = buffer[i];
                if (b >= 0x80)
                {
                    isAscii = false;
                }

                if (countChars && (b & 0xC0) != 0x80)
                {
                    charCount++; // a new character starts here
                }

                if (b == (byte)'\n')
                {
                    byteStarts.Add(position + i + 1);
                    if (countChars)
                    {
                        charStarts.Add(charCount);
                    }
                }
            }

            position += read;
            if (progress is not null && length > 0 && position - lastReported >= (1 << 20))
            {
                lastReported = position;
                progress.Report(Math.Min(1.0, (double)position / length));
            }
        }

        progress?.Report(1.0);
        return (byteStarts.ToArray(), charStarts.ToArray(), countChars ? charCount : -1, isAscii);
    }

    private static async Task<(Encoding Encoding, int Preamble)> DetectEncodingAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8, FileOptions.Asynchronous);
        byte[] head = new byte[4];
        int got = await stream.ReadAsync(head.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
        var span = head.AsSpan(0, got);

        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
        {
            return (new UTF8Encoding(true), 3);
        }

        if (span.Length >= 2 && span[0] == 0xFF && span[1] == 0xFE)
        {
            return (new UnicodeEncoding(false, true), 2);
        }

        if (span.Length >= 2 && span[0] == 0xFE && span[1] == 0xFF)
        {
            return (new UnicodeEncoding(true, true), 2);
        }

        return (new UTF8Encoding(false), 0);
    }

    public void Dispose()
    {
        _view.Dispose();
        _map.Dispose();
    }
}
