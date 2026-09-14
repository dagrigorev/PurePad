using PurePad.Editor;

namespace PurePad.Tests.Fakes;

/// <summary>
/// A lightweight, UI-free <see cref="ITextEditor"/> backed by a string. It lets the
/// search logic be tested without a WinForms control, demonstrating the value of the
/// editor abstraction (Dependency Inversion).
/// </summary>
internal sealed class InMemoryTextEditor : ITextEditor
{
    private string _text;
    private int _selectionStart;
    private int _selectionLength;

    public InMemoryTextEditor(string text = "")
    {
        _text = text;
    }

    public event EventHandler? TextChanged;

    public event EventHandler? SelectionChanged;

    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            _selectionStart = Math.Min(_selectionStart, _text.Length);
            _selectionLength = 0;
            TextChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void LoadText(string text)
    {
        Text = text;
        Modified = false;
    }

    public int TextLength => _text.Length;

    public string SelectedText => _text.Substring(_selectionStart, _selectionLength);

    public int SelectionStart => _selectionStart;

    public int SelectionLength => _selectionLength;

    public bool Modified { get; set; }

    public bool HasSelection => _selectionLength > 0;

    public bool CanUndo => false;

    public bool CanPaste => false;

    public int LineCount => Math.Max(1, _text.Split('\n').Length);

    public void Undo() { }

    public void Cut() { }

    public void Copy() { }

    public void Paste() { }

    public void SelectAll() => Select(0, _text.Length);

    public void DeleteSelection()
    {
        if (_selectionLength > 0)
        {
            InsertText(string.Empty);
        }
    }

    public void InsertText(string text)
    {
        _text = _text[.._selectionStart] + text + _text[(_selectionStart + _selectionLength)..];
        _selectionStart += text.Length;
        _selectionLength = 0;
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Select(int start, int length)
    {
        _selectionStart = start;
        _selectionLength = length;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ScrollToCaret() { }

    public int GetFirstCharIndexOfLine(int lineIndex)
    {
        string[] lines = _text.Split('\n');
        if (lineIndex < 0 || lineIndex >= lines.Length)
        {
            return -1;
        }

        int index = 0;
        for (int i = 0; i < lineIndex; i++)
        {
            index += lines[i].Length + 1;
        }

        return index;
    }

    public CaretPosition GetCaretPosition()
    {
        int line = 0;
        int column = 1;
        for (int i = 0; i < _selectionStart && i < _text.Length; i++)
        {
            if (_text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return new CaretPosition(line + 1, column);
    }
}
