namespace PurePad.Editor;

/// <summary>
/// The caret's location expressed in 1-based line/column coordinates for the status bar.
/// </summary>
public readonly record struct CaretPosition(int Line, int Column);

/// <summary>
/// Abstraction over the editing surface. Commands and the controller operate against this
/// interface instead of a concrete <c>RichTextBox</c>, so editing logic depends on
/// behaviour, not on a specific WinForms control (Dependency Inversion). It exposes only
/// what the application needs (Interface Segregation).
/// </summary>
public interface ITextEditor
{
    string Text { get; set; }

    /// <summary>
    /// Replace the whole buffer efficiently: painting is suspended during the swap and the
    /// undo history is cleared. Preferred over <see cref="Text"/> for loading a file.
    /// </summary>
    void LoadText(string text);

    int TextLength { get; }

    string SelectedText { get; }

    int SelectionStart { get; }

    int SelectionLength { get; }

    /// <summary>Whether the user has changed the text since <see cref="Modified"/> was last cleared.</summary>
    bool Modified { get; set; }

    bool HasSelection { get; }

    bool CanUndo { get; }

    bool CanPaste { get; }

    /// <summary>Raised after the text content changes.</summary>
    event EventHandler TextChanged;

    /// <summary>Raised after the selection or caret position changes.</summary>
    event EventHandler SelectionChanged;

    void Undo();

    void Cut();

    void Copy();

    void Paste();

    void SelectAll();

    /// <summary>Delete the current selection (no-op when nothing is selected).</summary>
    void DeleteSelection();

    /// <summary>Insert <paramref name="text"/> at the caret, replacing any selection.</summary>
    void InsertText(string text);

    void Select(int start, int length);

    void ScrollToCaret();

    /// <summary>Total number of lines (never less than one).</summary>
    int LineCount { get; }

    /// <summary>Index of the first character of <paramref name="lineIndex"/> (0-based line).</summary>
    int GetFirstCharIndexOfLine(int lineIndex);

    /// <summary>1-based line/column of the caret, for display in the status bar.</summary>
    CaretPosition GetCaretPosition();
}
