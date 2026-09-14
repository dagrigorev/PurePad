using System.Text;

namespace PurePad.LargeFiles;

/// <summary>
/// A piece-table text buffer: the document is a sequence of "pieces", each pointing into
/// either the immutable original text (<see cref="IOriginalText"/>) or an append-only add
/// buffer. Inserting and deleting only splices piece references, so an edit never copies
/// the whole document — the key to editing very large files cheaply. A line index is kept
/// incrementally so the viewer can fetch any visible line without rescanning.
/// </summary>
public sealed class PieceTable
{
    private enum Source
    {
        Original,
        Add,
    }

    private readonly record struct Piece(Source Source, int Start, int Length);

    /// <summary>An applied edit, kept so it can be inverted for undo/redo.</summary>
    private abstract record EditOp;

    private sealed record InsertOp(int Pos, string Text) : EditOp;

    private sealed record DeleteOp(int Pos, string Text) : EditOp;

    private readonly IOriginalText _original;
    private readonly StringBuilder _add = new();
    private readonly List<Piece> _pieces = new();
    private readonly List<int> _lineStarts = new();
    private readonly Stack<EditOp> _undo = new();
    private readonly Stack<EditOp> _redo = new();

    public PieceTable(IOriginalText original, IReadOnlyList<int>? originalLineStarts = null)
    {
        _original = original ?? throw new ArgumentNullException(nameof(original));

        if (original.Length > 0)
        {
            _pieces.Add(new Piece(Source.Original, 0, original.Length));
        }

        Length = original.Length;

        // A line always starts at 0; the rest come from the original's newline positions.
        _lineStarts.Add(0);
        if (originalLineStarts is not null)
        {
            foreach (int start in originalLineStarts)
            {
                if (start > 0 && start <= Length)
                {
                    _lineStarts.Add(start);
                }
            }
        }
        else
        {
            IndexOriginalLines();
        }
    }

    /// <summary>Total character count.</summary>
    public int Length { get; private set; }

    /// <summary>Number of lines (always at least one).</summary>
    public int LineCount => _lineStarts.Count;

    /// <summary>Character offset at which line <paramref name="index"/> starts.</summary>
    public int LineStart(int index) => _lineStarts[index];

    /// <summary>The full document text (only safe for modest sizes; used by the small-file path).</summary>
    public string GetAllText() => GetText(0, Length);

    /// <summary>
    /// Stream the whole document to <paramref name="writer"/> in chunks, so saving a huge
    /// edited file never builds one giant string. Reports fractional progress.
    /// </summary>
    public void WriteTo(TextWriter writer, IProgress<double>? progress = null)
    {
        const int chunk = 1 << 20; // 1M chars per write

        for (int pos = 0; pos < Length; pos += chunk)
        {
            int take = Math.Min(chunk, Length - pos);
            writer.Write(GetText(pos, take));
            progress?.Report(Length > 0 ? Math.Min(1.0, (double)(pos + take) / Length) : 1.0);
        }
    }

    /// <summary>Return the characters in <c>[position, position + count)</c>.</summary>
    public string GetText(int position, int count)
    {
        if (count <= 0 || position >= Length)
        {
            return string.Empty;
        }

        count = Math.Min(count, Length - position);
        var builder = new StringBuilder(count);

        int cursor = 0;
        int remaining = count;
        int readFrom = position;

        foreach (Piece piece in _pieces)
        {
            if (remaining <= 0)
            {
                break;
            }

            int pieceEnd = cursor + piece.Length;
            if (readFrom < pieceEnd)
            {
                int localStart = readFrom - cursor;
                int take = Math.Min(piece.Length - localStart, remaining);
                builder.Append(ReadPiece(piece, localStart, take));
                remaining -= take;
                readFrom += take;
            }

            cursor = pieceEnd;
        }

        return builder.ToString();
    }

    /// <summary>Text of line <paramref name="index"/> without its trailing line break.</summary>
    public string GetLine(int index)
    {
        int start = _lineStarts[index];
        int end = index + 1 < _lineStarts.Count ? _lineStarts[index + 1] : Length;
        string line = GetText(start, end - start);

        int trim = line.Length;
        if (trim > 0 && line[trim - 1] == '\n') trim--;
        if (trim > 0 && line[trim - 1] == '\r') trim--;
        return trim == line.Length ? line : line[..trim];
    }

    /// <summary>0-based line containing <paramref name="position"/>.</summary>
    public int LineFromPosition(int position)
    {
        int lo = 0, hi = _lineStarts.Count - 1, result = 0;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (_lineStarts[mid] <= position)
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

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Insert <paramref name="text"/> at <paramref name="position"/> (a recorded, undoable edit).</summary>
    public void Insert(int position, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        position = Math.Clamp(position, 0, Length);
        InsertCore(position, text);
        _redo.Clear();
        RecordInsert(position, text);
    }

    /// <summary>Delete <paramref name="count"/> characters at <paramref name="position"/> (a recorded, undoable edit).</summary>
    public void Delete(int position, int count)
    {
        if (count <= 0 || position >= Length)
        {
            return;
        }

        count = Math.Min(count, Length - position);
        string removed = GetText(position, count);
        DeleteCore(position, count);
        _redo.Clear();
        RecordDelete(position, removed);
    }

    /// <summary>Undo the most recent edit. Returns the caret position afterwards, or -1 if nothing to undo.</summary>
    public int Undo()
    {
        if (_undo.Count == 0)
        {
            return -1;
        }

        EditOp op = _undo.Pop();
        int caret = op switch
        {
            InsertOp io => UndoInsert(io),
            DeleteOp doo => UndoDelete(doo),
            _ => -1,
        };

        _redo.Push(op);
        return caret;
    }

    /// <summary>Redo the most recently undone edit. Returns the caret position afterwards, or -1 if nothing to redo.</summary>
    public int Redo()
    {
        if (_redo.Count == 0)
        {
            return -1;
        }

        EditOp op = _redo.Pop();
        int caret = op switch
        {
            InsertOp io => RedoInsert(io),
            DeleteOp doo => RedoDelete(doo),
            _ => -1,
        };

        _undo.Push(op);
        return caret;
    }

    private int UndoInsert(InsertOp op) { DeleteCore(op.Pos, op.Text.Length); return op.Pos; }

    private int UndoDelete(DeleteOp op) { InsertCore(op.Pos, op.Text); return op.Pos + op.Text.Length; }

    private int RedoInsert(InsertOp op) { InsertCore(op.Pos, op.Text); return op.Pos + op.Text.Length; }

    private int RedoDelete(DeleteOp op) { DeleteCore(op.Pos, op.Text.Length); return op.Pos; }

    /// <summary>Coalesce single-character typing into the previous insert op for a natural undo unit.</summary>
    private void RecordInsert(int position, string text)
    {
        if (text.Length == 1 && text[0] is not ('\n' or '\r') &&
            _undo.Count > 0 && _undo.Peek() is InsertOp prev &&
            prev.Pos + prev.Text.Length == position && !prev.Text.EndsWith('\n'))
        {
            _undo.Pop();
            _undo.Push(prev with { Text = prev.Text + text });
            return;
        }

        _undo.Push(new InsertOp(position, text));
    }

    /// <summary>Coalesce contiguous single-character deletes (holding Backspace/Delete) into one op.</summary>
    private void RecordDelete(int position, string removed)
    {
        if (removed.Length == 1 && _undo.Count > 0 && _undo.Peek() is DeleteOp prev)
        {
            if (position + removed.Length == prev.Pos) // backspace: deleting the char before the previous
            {
                _undo.Pop();
                _undo.Push(new DeleteOp(position, removed + prev.Text));
                return;
            }

            if (position == prev.Pos) // forward delete at the same spot
            {
                _undo.Pop();
                _undo.Push(new DeleteOp(prev.Pos, prev.Text + removed));
                return;
            }
        }

        _undo.Push(new DeleteOp(position, removed));
    }

    // ----- internals ------------------------------------------------------

    private void InsertCore(int position, string text)
    {
        int addStart = _add.Length;
        _add.Append(text);
        var piece = new Piece(Source.Add, addStart, text.Length);

        int index = SplitAt(position);
        _pieces.Insert(index, piece);
        Length += text.Length;

        UpdateLineStartsForInsert(position, text);
    }

    private void DeleteCore(int position, int count)
    {
        int startIndex = SplitAt(position);
        int endIndex = SplitAt(position + count);
        _pieces.RemoveRange(startIndex, endIndex - startIndex);
        Length -= count;

        UpdateLineStartsForDelete(position, count);
    }

    private string ReadPiece(Piece piece, int offset, int count) => piece.Source == Source.Original
        ? _original.GetText(piece.Start + offset, count)
        : _add.ToString(piece.Start + offset, count);

    /// <summary>Ensure a piece boundary exists at <paramref name="position"/>; return that boundary's piece index.</summary>
    private int SplitAt(int position)
    {
        if (position <= 0)
        {
            return 0;
        }

        int cursor = 0;
        for (int i = 0; i < _pieces.Count; i++)
        {
            Piece piece = _pieces[i];
            if (position == cursor)
            {
                return i;
            }

            if (position < cursor + piece.Length)
            {
                int left = position - cursor;
                _pieces[i] = piece with { Length = left };
                _pieces.Insert(i + 1, new Piece(piece.Source, piece.Start + left, piece.Length - left));
                return i + 1;
            }

            cursor += piece.Length;
        }

        return _pieces.Count; // position == Length
    }

    private void IndexOriginalLines()
    {
        // Scan the original once for newlines. Only used for small in-memory sources; the
        // large-file path passes a precomputed index instead.
        string text = _original.GetText(0, _original.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                _lineStarts.Add(i + 1);
            }
        }
    }

    private void UpdateLineStartsForInsert(int position, string text)
    {
        int delta = text.Length;

        for (int i = 0; i < _lineStarts.Count; i++)
        {
            if (_lineStarts[i] > position)
            {
                _lineStarts[i] += delta;
            }
        }

        for (int k = 0; k < text.Length; k++)
        {
            if (text[k] == '\n')
            {
                InsertLineStart(position + k + 1);
            }
        }
    }

    private void UpdateLineStartsForDelete(int position, int count)
    {
        int end = position + count;
        _lineStarts.RemoveAll(s => s > position && s <= end);

        for (int i = 0; i < _lineStarts.Count; i++)
        {
            if (_lineStarts[i] > position)
            {
                _lineStarts[i] -= count;
            }
        }
    }

    private void InsertLineStart(int value)
    {
        int index = _lineStarts.BinarySearch(value);
        if (index < 0)
        {
            _lineStarts.Insert(~index, value);
        }
    }
}
