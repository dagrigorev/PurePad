using System.Text;
using System.Windows.Forms;
using PurePad.Editor;
using PurePad.LargeFiles;

namespace PurePad.View.Controls;

/// <summary>
/// <see cref="ITextEditor"/> implementation for the viewer, so it is the single editing
/// surface for files of any size. Small files are held in an in-memory piece table (built
/// by <see cref="LoadText"/>); large files are backed by the memory-mapped document. The
/// whole-document <see cref="Text"/> accessor is only used by size-gated features.
/// </summary>
public sealed partial class LargeFileViewer
{
    public new event EventHandler? TextChanged;

    public event EventHandler? SelectionChanged;

    /// <summary>Whole-document text. Only invoked for reasonably sized documents (callers are size-gated).</summary>
    public new string Text
    {
        get
        {
            if (_table is not null)
            {
                var writer = new StringWriter();
                _table.WriteTo(writer);
                return writer.ToString();
            }

            if (_document is not null && _document.CharLength is > 0 and <= int.MaxValue)
            {
                return _document.GetTextByChars(0, (int)_document.CharLength);
            }

            return string.Empty;
        }
        set => LoadText(value ?? string.Empty);
    }

    /// <summary>Load a small document into an in-memory, fully editable piece table.</summary>
    public void LoadText(string text)
    {
        _table = new PieceTable(new StringOriginalText(text ?? string.Empty));
        _document = null;
        _dirty = false;
        ResetView();
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    public int TextLength => TotalChars;

    public string SelectedText
    {
        get
        {
            if (!HasSelection)
            {
                return string.Empty;
            }

            int start = SelectionStart;
            return ReadRange(start, SelectionLength);
        }
    }

    public int SelectionStart
    {
        get
        {
            (int sl, int sc, _, _) = NormalizedSelection();
            return LineColumnToPosition(sl, sc);
        }
    }

    public int SelectionLength
    {
        get
        {
            (int sl, int sc, int el, int ec) = NormalizedSelection();
            return LineColumnToPosition(el, ec) - LineColumnToPosition(sl, sc);
        }
    }

    public bool Modified
    {
        get => _dirty;
        set
        {
            _dirty = value;
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool CanUndo => _table?.CanUndo ?? false;

    public bool CanPaste
    {
        get
        {
            try { return IsEditable && Clipboard.ContainsText(); }
            catch { return false; }
        }
    }

    public void Cut() => CutSelection();

    public void Copy() => CopySelection();

    public void Paste() => PasteAtCaret();

    public void DeleteSelection()
    {
        if (IsEditable && HasSelection)
        {
            DeleteSelectionInternal();
            OnEdited();
        }
    }

    /// <summary>Select the range <c>[start, start + length)</c> and reveal the caret.</summary>
    public void Select(int start, int length)
    {
        int total = TotalChars;
        start = Math.Clamp(start, 0, total);
        int end = Math.Clamp(start + Math.Max(0, length), 0, total);

        (_anchorLine, _anchorCol) = PositionToLineColumn(start);
        (_caretLine, _caretCol) = PositionToLineColumn(end);
        EnsureCaretVisible();
        UpdateBracketMatch();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    public void ScrollToCaret()
    {
        EnsureCaretVisible();
        Invalidate();
    }

    public int GetFirstCharIndexOfLine(int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= LineCount)
        {
            return -1;
        }

        return _table is not null ? _table.LineStart(lineIndex) : _document?.GetLineCharStart(lineIndex) ?? 0;
    }

    public CaretPosition GetCaretPosition() => new(_caretLine + 1, _caretCol + 1);

    /// <summary>Char offset of (line, column), clamping the column to the line's content length.</summary>
    private int LineColumnToPosition(int line, int column)
    {
        line = Math.Clamp(line, 0, Math.Max(0, LineCount - 1));
        int col = Math.Min(column, ContentLength(line));
        return GetFirstCharIndexOfLine(line) + col;
    }
}
