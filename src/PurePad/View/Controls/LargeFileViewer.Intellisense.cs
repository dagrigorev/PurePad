using System.Drawing;
using System.Windows.Forms;
using PurePad.Editor;

namespace PurePad.View.Controls;

/// <summary>
/// Lightweight IDE assists driven by <see cref="DocumentIndex"/>: identifier completion while
/// typing, Ctrl+click go-to-definition (and open-include), and hover documentation. The index is
/// rebuilt on a short debounce after edits, and only for reasonably sized documents so huge files
/// stay cheap.
/// </summary>
public sealed partial class LargeFileViewer
{
    private const int IndexSizeLimit = 4_000_000; // don't index documents larger than this

    private DocumentIndex? _index;
    private readonly System.Windows.Forms.Timer _indexTimer = new() { Interval = 300 };

    private readonly ListBox _completionList = new()
    {
        Visible = false,
        IntegralHeight = false,
        BorderStyle = BorderStyle.FixedSingle,
        TabStop = false,
    };
    private bool _completionOpen;
    private int _completionStartCol;
    private IReadOnlyList<string> _completionWords = Array.Empty<string>();

    /// <summary>Set the language's static completion vocabulary (keywords + profile completions).</summary>
    public void SetCompletionWords(IReadOnlyList<string> words) => _completionWords = words ?? Array.Empty<string>();

    private readonly System.Windows.Forms.Timer _hoverTimer = new() { Interval = 550 };
    private readonly ToolTip _hoverTip = new() { UseFading = false, UseAnimation = false };
    private Point _hoverPoint;
    private string _hoverWord = string.Empty;

    /// <summary>Raised when Ctrl+click lands on an <c>#include</c> target; the host resolves the path.</summary>
    public event EventHandler<string>? IncludeOpenRequested;

    private void InitIntellisense()
    {
        _indexTimer.Tick += (s, e) => { _indexTimer.Stop(); RebuildIndex(); };
        _hoverTimer.Tick += (s, e) => { _hoverTimer.Stop(); ShowHoverDoc(); };

        _completionList.Click += (s, e) => AcceptCompletion();
        _completionList.MouseDoubleClick += (s, e) => AcceptCompletion();
        Controls.Add(_completionList);
    }

    private void ApplyIntellisenseColors()
    {
        _completionList.BackColor = _isDark ? ControlPaint.Light(_background, 0.10f) : Color.White;
        _completionList.ForeColor = _foreground;
        _hoverTip.BackColor = _isDark ? ControlPaint.Light(_background, 0.12f) : SystemColors.Info;
        _hoverTip.ForeColor = _foreground;
        _hoverTip.OwnerDraw = false;
    }

    // ----- indexing -------------------------------------------------------

    private void ScheduleIndex()
    {
        _indexTimer.Stop();
        _indexTimer.Start();
    }

    private void RebuildIndex()
    {
        if (!IsEditable || TotalChars > IndexSizeLimit)
        {
            _index = null;
            return;
        }

        try
        {
            _index = DocumentIndex.Build(Text);
        }
        catch (Exception ex) when (ex is OutOfMemoryException or InvalidOperationException)
        {
            _index = null;
        }
    }

    // ----- completion -----------------------------------------------------

    /// <summary>The identifier prefix ending at the caret, and the column it starts on.</summary>
    private string CurrentPrefix(out int startCol)
    {
        string head = GetSlice(_caretLine, 0, Math.Min(ContentLength(_caretLine), 4096));
        int end = Math.Min(_caretCol, head.Length);
        int start = end;
        while (start > 0 && (char.IsLetterOrDigit(head[start - 1]) || head[start - 1] == '_'))
        {
            start--;
        }

        startCol = start;
        return head[start..end];
    }

    /// <summary>Refresh the completion popup for the caret's current prefix.</summary>
    private void UpdateCompletion()
    {
        if (!IsEditable || _index is null || HasSelection)
        {
            HideCompletion();
            return;
        }

        string prefix = CurrentPrefix(out int startCol);
        if (prefix.Length < 2)
        {
            HideCompletion();
            return;
        }

        IReadOnlyList<string> hits = _index.CompletionsFor(prefix, _completionWords);
        if (hits.Count == 0)
        {
            HideCompletion();
            return;
        }

        _completionStartCol = startCol;
        _completionList.BeginUpdate();
        _completionList.Items.Clear();
        foreach (string h in hits)
        {
            _completionList.Items.Add(h);
        }

        _completionList.SelectedIndex = 0;
        _completionList.EndUpdate();
        PositionCompletion();
        _completionList.Visible = true;
        _completionOpen = true;
    }

    private void PositionCompletion()
    {
        int row = RowOf(_caretLine);
        if (row < 0)
        {
            row = 0;
        }

        int itemHeight = Math.Max(1, _completionList.ItemHeight);
        int height = Math.Min(_completionList.Items.Count, 8) * itemHeight + 6;
        int width = 260;
        int x = Math.Min(TextLeft + _completionStartCol * _charWidth - _hOffset, Width - width - 4);
        int y = (row + 1) * _lineHeight;
        if (y + height > Height)
        {
            y = row * _lineHeight - height; // flip above the line when it would overflow
        }

        _completionList.SetBounds(Math.Max(0, x), Math.Max(0, y), width, height);
    }

    private void HideCompletion()
    {
        if (_completionOpen)
        {
            _completionOpen = false;
            _completionList.Visible = false;
        }
    }

    /// <summary>Handle a key while the completion popup is open. Returns true when consumed.</summary>
    private bool HandleCompletionKey(Keys key)
    {
        if (!_completionOpen)
        {
            return false;
        }

        switch (key)
        {
            case Keys.Down:
                _completionList.SelectedIndex = Math.Min(_completionList.SelectedIndex + 1, _completionList.Items.Count - 1);
                return true;
            case Keys.Up:
                _completionList.SelectedIndex = Math.Max(_completionList.SelectedIndex - 1, 0);
                return true;
            case Keys.Enter:
            case Keys.Tab:
                AcceptCompletion();
                return true;
            case Keys.Escape:
                HideCompletion();
                return true;
            default:
                return false;
        }
    }

    private void AcceptCompletion()
    {
        if (!_completionOpen || _completionList.SelectedItem is not string word)
        {
            return;
        }

        HideCompletion();
        // Replace the typed prefix [startCol, caretCol) with the chosen word.
        _anchorLine = _caretLine;
        _anchorCol = _completionStartCol;
        InsertText(word);
        Focus();
    }

    // ----- go-to-definition (Ctrl+click) ----------------------------------

    /// <summary>Handle Ctrl+click: navigate to a definition, or ask the host to open an include.</summary>
    private bool TryGoToDefinition(Point location)
    {
        (int line, int col) = PointToPosition(location);
        string lineText = GetSlice(line, 0, Math.Min(ContentLength(line), 8192));

        if (DocumentIndex.IncludeTargetAt(lineText, col) is { } include)
        {
            IncludeOpenRequested?.Invoke(this, include);
            return true;
        }

        string word = DocumentIndex.WordAt(lineText, col);
        if (word.Length == 0 || _index is null || !_index.TryGetDefinition(word, out SymbolDefinition def))
        {
            return false;
        }

        if (def.Kind == SymbolKind.Include)
        {
            IncludeOpenRequested?.Invoke(this, def.Signature);
            return true;
        }

        // Select the definition's name on its line and reveal it.
        int nameCol = Math.Max(0, def.Signature.IndexOf(word, StringComparison.Ordinal));
        RevealLine(def.Line);
        _anchorLine = _caretLine = def.Line;
        _anchorCol = nameCol;
        _caretCol = nameCol + word.Length;
        EnsureCaretVisible();
        UpdateBracketMatch();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
        return true;
    }

    // ----- hover documentation --------------------------------------------

    private void ScheduleHover(Point location)
    {
        if (_index is null || location.X < TextLeft)
        {
            _hoverTip.Hide(this);
            _hoverWord = string.Empty;
            _hoverTimer.Stop();
            return;
        }

        // Staying on the same word must not re-trigger: hiding + rescheduling on every mouse
        // jitter is what made the tooltip blink.
        (int line, int col) = PointToPosition(location);
        string word = DocumentIndex.WordAt(GetSlice(line, 0, Math.Min(ContentLength(line), 8192)), col);
        if (word.Length > 0 && word == _hoverWord)
        {
            return; // tooltip already shown for this word — leave it be
        }

        _hoverTip.Hide(this);
        _hoverWord = string.Empty;
        _hoverTimer.Stop();
        _hoverPoint = location;
        _hoverTimer.Start();
    }

    private void ShowHoverDoc()
    {
        if (_index is null)
        {
            return;
        }

        (int line, int col) = PointToPosition(_hoverPoint);
        string lineText = GetSlice(line, 0, Math.Min(ContentLength(line), 8192));
        string word = DocumentIndex.WordAt(lineText, col);
        if (word.Length == 0 || !_index.TryGetDefinition(word, out SymbolDefinition def) || def.Line == line)
        {
            return;
        }

        string kind = def.Kind.ToString().ToLowerInvariant();
        string tip = $"({kind}) {def.Signature}";
        if (def.Doc.Length > 0)
        {
            tip += "\n\n" + def.Doc;
        }

        _hoverWord = word;
        _hoverTip.Show(tip, this, _hoverPoint.X + 12, _hoverPoint.Y + 18, 8000);
    }

    private void DisposeIntellisense()
    {
        _indexTimer.Dispose();
        _hoverTimer.Dispose();
        _hoverTip.Dispose();
        _completionList.Dispose();
    }
}
