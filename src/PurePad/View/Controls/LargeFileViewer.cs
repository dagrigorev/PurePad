using System.Drawing;
using System.Drawing.Text;
using System.Text;
using System.Windows.Forms;
using PurePad.Editor;
using PurePad.Formatting;
using PurePad.LargeFiles;

namespace PurePad.View.Controls;

/// <summary>
/// A virtualized view of a very large file. It renders only the lines visible in the
/// viewport, so scrolling a 500 MB file costs the same as a small one. When given a
/// <see cref="PieceTable"/> it is fully editable (type, delete, cut/copy/paste) — edits
/// splice piece references rather than rewriting the file — otherwise it is a read-only
/// viewer over the memory-mapped document. It supports two-axis scrolling and mouse/keyboard
/// selection, draws its own line-number gutter, and follows the active theme.
/// </summary>
public sealed partial class LargeFileViewer : Control, ITextEditor
{
    private const int GutterPadding = 8;
    private const int MaxCopyChars = 20_000_000;

    private readonly FlatScrollBar _vScroll = new(vertical: true);
    private readonly FlatScrollBar _hScroll = new(vertical: false);

    private LargeFileDocument? _document; // read-only source (also the piece table's original)
    private PieceTable? _table;           // editable buffer (null => read-only)
    private bool _dirty;

    private int _topLine;
    private int _hOffset;
    private int _lineHeight = 16;
    private int _charWidth = 8;
    private int _gutterWidth = 52;
    private int _maxLineChars = 1;

    private int _anchorLine, _anchorCol, _caretLine, _caretCol;
    private bool _selecting;

    private Color _background = SystemColors.Window;
    private Color _foreground = SystemColors.WindowText;
    private Color _gutterBackground = Color.FromArgb(240, 240, 240);
    private Color _gutterForeground = Color.FromArgb(110, 110, 110);
    private Color _selection = Color.FromArgb(120, 51, 153, 255);
    private Color _currentLineFill = Color.FromArgb(24, 51, 153, 255);
    private Color _bracketColor = Color.FromArgb(51, 153, 255);

    private ISyntaxHighlighter? _highlighter;
    private SyntaxTheme? _syntaxTheme;

    private bool _antialias = true;
    private TextRenderingHint TextHint => _antialias ? TextRenderingHint.ClearTypeGridFit : TextRenderingHint.SingleBitPerPixelGridFit;

    /// <summary>Whether editor text is drawn antialiased (ClearType) or hard-edged.</summary>
    public bool TextAntialiasing
    {
        get => _antialias;
        set { if (_antialias != value) { _antialias = value; Invalidate(); } }
    }

    // Typographic layout keeps GDI+ glyph advances on the monospace grid (matches TextRenderer metrics).
    private static readonly StringFormat GlyphFormat = new(StringFormat.GenericTypographic)
    {
        FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip,
    };

    private (int Open, int Close)? _bracketMatch; // char positions, when the caret is by a bracket
    private const int MaxBracketScan = 200_000;

    private readonly System.Windows.Forms.Timer _caretTimer = new();
    private readonly System.Windows.Forms.Timer _dragScrollTimer = new() { Interval = 40 };
    private bool _caretOn = true;
    private Point _dragPoint;

    public LargeFileViewer()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        BackColor = _background;
        Font = new Font(PickCodeFontFamily(), 10.5f);
        _lineHeight = Font.Height;

        _vScroll.Scroll += (s, e) => { _topLine = LineAtDisplay(_vScroll.Value); Invalidate(); };
        _hScroll.Scroll += (s, e) => { _hOffset = _hScroll.Value; Invalidate(); };
        Controls.Add(_vScroll);
        Controls.Add(_hScroll);

        _caretTimer.Interval = Math.Max(200, SystemInformation.CaretBlinkTime);
        _caretTimer.Tick += (s, e) => { _caretOn = !_caretOn; InvalidateCaretRegion(); };
        _dragScrollTimer.Tick += (s, e) => DragScrollTick();
    }

    /// <summary>Raised on the first edit and whenever content changes.</summary>
    /// <summary>Raised when the dirty state may have changed (kept distinct from the <see cref="Modified"/> flag).</summary>
    public event EventHandler? ModifiedChanged;

    public bool IsEditable => _table is not null;

    public bool IsDirty => _dirty;

    public string? FilePath => _document?.Path;

    public int LineCount => _table?.LineCount ?? _document?.LineCount ?? 0;

    public int TopLine => _topLine + 1;

    /// <summary>Scroll so <paramref name="oneBasedLine"/> is near the top (debug/navigation helper).</summary>
    public void GoToLine(int oneBasedLine) => ScrollToLine(Math.Max(0, oneBasedLine - 1));

    /// <summary>Debug helper: collapse/expand the block headed at <paramref name="oneBasedLine"/>.</summary>
    public void DebugToggleFold(int oneBasedLine) => ToggleFold(Math.Max(0, oneBasedLine - 1));

    /// <summary>Debug helper: place the caret and refresh bracket matching (for screenshots).</summary>
    public void DebugCaretAt(int zeroBasedLine, int column)
    {
        _caretLine = _anchorLine = Math.Clamp(zeroBasedLine, 0, Math.Max(0, LineCount - 1));
        _caretCol = _anchorCol = Math.Max(0, column);
        UpdateBracketMatch();
        Invalidate();
    }

    /// <summary>Show a file read-only (no piece table).</summary>
    public void SetDocument(LargeFileDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _table = null;
        _dirty = false;
        ResetView();
    }

    /// <summary>Show a file for editing, backed by <paramref name="table"/> over <paramref name="document"/>.</summary>
    public void SetEditable(LargeFileDocument document, PieceTable table)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _table = table ?? throw new ArgumentNullException(nameof(table));
        _dirty = false;
        ResetView();
    }

    private void ResetView()
    {
        ClearFolds();
        _topLine = _hOffset = 0;
        _maxLineChars = 1;
        _anchorLine = _anchorCol = _caretLine = _caretCol = 0;
        MeasureFontMetrics();
        _gutterWidth = MeasureGutter();
        UpdateScrollRanges();
        Invalidate();
    }

    public void MarkSaved() { _dirty = false; }

    /// <summary>Release the current document/buffer (e.g. before its file is replaced on save).</summary>
    public void Clear()
    {
        _table = null;
        _document = null;
        _dirty = false;
        Invalidate();
    }

    public void WriteTo(TextWriter writer, IProgress<double>? progress = null) => _table?.WriteTo(writer, progress);

    public void ApplyColors(Color background, Color foreground, Color gutterBackground, Color gutterForeground, Color accent)
    {
        _background = background;
        _foreground = foreground;
        _gutterBackground = gutterBackground;
        _gutterForeground = gutterForeground;
        bool dark = background.GetBrightness() < 0.5f;
        _currentLineFill = Color.FromArgb(dark ? 38 : 26, accent);
        _bracketColor = dark ? ControlPaint.Light(accent, 0.5f) : accent;
        RefreshOutlineColors();

        // Scrollbars share the window background; the thumb is a subtle contrasting shade.
        Color thumb = dark ? ControlPaint.Light(background, 0.22f) : ControlPaint.Dark(background, 0.16f);
        Color thumbHot = dark ? ControlPaint.Light(background, 0.34f) : ControlPaint.Dark(background, 0.28f);
        _vScroll.SetColors(background, thumb, thumbHot);
        _hScroll.SetColors(background, thumb, thumbHot);

        BackColor = background;
        Invalidate();
    }

    public void ApplyFont(Font font)
    {
        Font = font;
        DefaultFontSize = font.Size;
        MeasureFontMetrics();
        _gutterWidth = MeasureGutter();
        UpdateScrollRanges();
        Invalidate();
    }

    /// <summary>Set (or clear) per-line syntax colouring for the current document.</summary>
    public void SetHighlighting(ISyntaxHighlighter? highlighter, SyntaxTheme? theme)
    {
        _highlighter = highlighter;
        _syntaxTheme = theme;
        Invalidate();
    }

    /// <summary>Swap just the colour palette when the theme changes.</summary>
    public void UpdateSyntaxTheme(SyntaxTheme theme)
    {
        _syntaxTheme = theme;
        Invalidate();
    }

    // ----- line access ----------------------------------------------------

    /// <summary>Character length of a line (excluding CR/LF) computed without reading the whole line.</summary>
    private int ContentLength(int index)
    {
        if (_table is not null)
        {
            int ls = _table.LineStart(index);
            int le = index + 1 < _table.LineCount ? _table.LineStart(index + 1) : _table.Length;
            int breakLen = 0;
            if (le - 1 >= ls && _table.GetText(le - 1, 1) == "\n")
            {
                breakLen = 1;
                if (le - 2 >= ls && _table.GetText(le - 2, 1) == "\r")
                {
                    breakLen = 2;
                }
            }

            return (le - ls) - breakLen;
        }

        return _document?.LineContentLength(index) ?? 0;
    }

    /// <summary>Read only the visible slice of a line: <paramref name="count"/> chars from <paramref name="startColumn"/>.</summary>
    private string GetSlice(int index, int startColumn, int count)
    {
        if (_table is not null)
        {
            int content = ContentLength(index);
            if (startColumn >= content || count <= 0)
            {
                return string.Empty;
            }

            int take = Math.Min(count, content - startColumn);
            return _table.GetText(_table.LineStart(index) + startColumn, take);
        }

        return _document?.GetLineSlice(index, startColumn, count) ?? string.Empty;
    }

    private int FirstVisibleColumn => _charWidth > 0 ? _hOffset / _charWidth : 0;

    private int VisibleColumns => Math.Max(1, ContentWidth / Math.Max(1, _charWidth) + 2);

    /// <summary>The first installed of the preferred VS-style coding fonts (Consolas always exists on Windows).</summary>
    private static string PickCodeFontFamily()
    {
        foreach (string name in new[] { "Lucida Console", "Cascadia Code", "Cascadia Mono", "Consolas" })
        {
            try
            {
                using var probe = new FontFamily(name);
                return probe.Name;
            }
            catch (ArgumentException)
            {
                // not installed — try the next
            }
        }

        return FontFamily.GenericMonospace.Name;
    }

    private void MeasureFontMetrics()
    {
        _lineHeight = Math.Max(1, Font.Height);
        _charWidth = Math.Max(1, TextRenderer.MeasureText("0000", Font, Size.Empty, TextFormatFlags.NoPadding).Width / 4);
    }

    private int VisibleLines => Math.Max(1, (Height - (_hScroll.Visible ? _hScroll.Height : 0)) / Math.Max(1, _lineHeight));

    private int ContentWidth => Math.Max(0, Width - _gutterWidth - (_vScroll.Visible ? _vScroll.Width : 0));

    private int TextLeft => _gutterWidth + 2;

    private int MeasureGutter()
    {
        int digits = Math.Max(2, LineCount.ToString().Length);
        return TextRenderer.MeasureText(new string('8', digits), Font).Width + GutterPadding * 2 + FoldMarginWidth;
    }

    private void UpdateScrollRanges()
    {
        int visible = VisibleLines;
        int visibleTotal = VisibleLineTotal;
        _vScroll.Minimum = 0;
        _vScroll.LargeChange = visible;
        _vScroll.SmallChange = 1;
        _vScroll.Maximum = Math.Max(0, visibleTotal - 1);
        _vScroll.Value = Math.Clamp(DisplayIndexOf(_topLine), 0, _vScroll.Maximum);
        _vScroll.Visible = visibleTotal > visible;

        int contentPixels = _maxLineChars * _charWidth;
        _hScroll.Minimum = 0;
        _hScroll.LargeChange = Math.Max(1, ContentWidth);
        _hScroll.SmallChange = _charWidth;
        _hScroll.Maximum = Math.Max(0, contentPixels);
        _hScroll.Visible = contentPixels > ContentWidth;
        int hMax = Math.Max(0, _hScroll.Maximum - _hScroll.LargeChange + 1);
        if (_hOffset > hMax)
        {
            _hOffset = _hScroll.Value = hMax;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateScrollRanges();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if ((ModifierKeys & Keys.Control) != 0)
        {
            Zoom(e.Delta / 120); // Ctrl+wheel zooms, like Visual Studio
            return;
        }

        ScrollVisibleLines(-(e.Delta / 120) * 3);
    }

    private const float MinFontSize = 6f;
    private const float MaxFontSize = 48f;

    /// <summary>Grow or shrink the editor font by <paramref name="steps"/> points, keeping the top line.</summary>
    public void Zoom(int steps)
    {
        if (steps == 0)
        {
            return;
        }

        float size = Math.Clamp(Font.Size + steps, MinFontSize, MaxFontSize);
        if (Math.Abs(size - Font.Size) < 0.01f)
        {
            return;
        }

        int keepTop = _topLine;
        Font = new Font(Font.FontFamily, size, Font.Style);
        MeasureFontMetrics();
        _gutterWidth = MeasureGutter();
        _topLine = keepTop;
        UpdateScrollRanges();
        Invalidate();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reset the editor font to its default (unzoomed) size.</summary>
    public void ResetZoom()
    {
        if (Math.Abs(Font.Size - DefaultFontSize) < 0.01f)
        {
            return;
        }

        Font = new Font(Font.FontFamily, DefaultFontSize, Font.Style);
        MeasureFontMetrics();
        _gutterWidth = MeasureGutter();
        UpdateScrollRanges();
        Invalidate();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the zoom level (font size) changes.</summary>
    public event EventHandler? ZoomChanged;

    private float DefaultFontSize { get; set; } = 10.5f;

    /// <summary>The unzoomed font size (the size the user chose, before Ctrl+wheel zoom).</summary>
    public float BaseFontSize => DefaultFontSize;

    /// <summary>Current zoom offset in points relative to <see cref="BaseFontSize"/>.</summary>
    public int ZoomSteps => (int)Math.Round(Font.Size - DefaultFontSize);

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) switch
    {
        Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Left or Keys.Right => true,
        _ => base.IsInputKey(keyData),
    };

    // ----- keyboard -------------------------------------------------------

    /// <summary>
    /// Handle editing and clipboard keys here (not OnKeyDown) so the focused viewer wins over
    /// the main Edit menu's accelerators (Ctrl+Z/C/X/V/A, Delete, …), which fire in ProcessCmdKey.
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        bool ctrl = (keyData & Keys.Control) != 0;
        bool shift = (keyData & Keys.Shift) != 0;

        if (ctrl)
        {
            switch (key)
            {
                case Keys.C: CopySelection(); return true;
                case Keys.A: SelectAll(); return true;
                case Keys.X when IsEditable: CutSelection(); return true;
                case Keys.V when IsEditable: PasteAtCaret(); return true;
                case Keys.Z when IsEditable && shift: Redo(); return true;
                case Keys.Z when IsEditable: Undo(); return true;
                case Keys.Y when IsEditable: Redo(); return true;
                case Keys.Oemplus or Keys.Add: Zoom(+1); return true;
                case Keys.OemMinus or Keys.Subtract: Zoom(-1); return true;
                case Keys.D0 or Keys.NumPad0: ResetZoom(); return true;
                case Keys.M: ToggleFoldAtCaret(); return true; // collapse/expand the caret's block
            }
        }
        else if (IsEditable)
        {
            switch (key)
            {
                case Keys.Back: DeleteBackward(); return true;
                case Keys.Delete: DeleteForward(); return true;
                case Keys.Enter: InsertNewLineWithIndent(); return true;
                case Keys.Tab: InsertText("\t"); return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Navigation (moves the caret when editable, otherwise scrolls).
        bool extend = e.Shift && IsEditable;
        switch (e.KeyCode)
        {
            case Keys.Up: MoveCaret(_caretLine - 1, _caretCol, extend); break;
            case Keys.Down: MoveCaret(_caretLine + 1, _caretCol, extend); break;
            case Keys.Left: MoveCaret(_caretLine, _caretCol - 1, extend); break;
            case Keys.Right: MoveCaret(_caretLine, _caretCol + 1, extend); break;
            case Keys.Home: MoveCaret(_caretLine, 0, extend); break;
            case Keys.End: MoveCaret(_caretLine, ContentLength(_caretLine), extend); break;
            case Keys.PageUp: ScrollVisibleLines(-VisibleLines); break;
            case Keys.PageDown: ScrollVisibleLines(VisibleLines); break;
        }
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (IsEditable && !char.IsControl(e.KeyChar))
        {
            InsertText(e.KeyChar.ToString());
            e.Handled = true;
        }
    }

    // ----- mouse & selection ---------------------------------------------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        // A click on a pinned sticky header jumps to that block instead of moving the caret.
        if (e.Button == MouseButtons.Left && TryStickyClick(e.Location))
        {
            return;
        }

        // A click on a fold marker collapses/expands that block.
        if (e.Button == MouseButtons.Left && TryFoldMarginClick(e.Location))
        {
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            (_caretLine, _caretCol) = PointToPosition(e.Location);

            // Shift+click extends the current selection instead of starting a new one.
            if ((ModifierKeys & Keys.Shift) == 0)
            {
                (_anchorLine, _anchorCol) = (_caretLine, _caretCol);
            }

            _selecting = true;
            ResetCaretBlink();
            UpdateBracketMatch();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        // Hand over a pinned header, I-beam over the text, arrow over the gutter.
        bool overSticky = _stickyEnabled && e.Y < _stickyHits.Count * _lineHeight;
        Cursor = overSticky ? Cursors.Hand : e.X >= _gutterWidth ? Cursors.IBeam : Cursors.Default;

        if (_selecting)
        {
            _dragPoint = e.Location;
            (_caretLine, _caretCol) = PointToPosition(e.Location);
            // Auto-scroll while dragging past the top/bottom edge.
            _dragScrollTimer.Enabled = e.Y < 0 || e.Y > ClientSize.Height;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _selecting = false;
        _dragScrollTimer.Enabled = false;
    }

    private void DragScrollTick()
    {
        if (!_selecting)
        {
            _dragScrollTimer.Enabled = false;
            return;
        }

        if (_dragPoint.Y < 0)
        {
            ScrollToLine(_topLine - 1);
        }
        else if (_dragPoint.Y > ClientSize.Height)
        {
            ScrollToLine(_topLine + 1);
        }

        (_caretLine, _caretCol) = PointToPosition(_dragPoint);
        Invalidate();
    }

    private (int Line, int Column) PointToPosition(Point p)
    {
        int line = LineAtRow(Math.Max(0, p.Y / _lineHeight));
        int column = Math.Max(0, (p.X - TextLeft + _hOffset + _charWidth / 2) / _charWidth);
        column = Math.Min(column, ContentLength(line));
        return (line, column);
    }

    public bool HasSelection => _anchorLine != _caretLine || _anchorCol != _caretCol;

    private (int SL, int SC, int EL, int EC) NormalizedSelection()
    {
        bool anchorFirst = _anchorLine < _caretLine || (_anchorLine == _caretLine && _anchorCol <= _caretCol);
        return anchorFirst
            ? (_anchorLine, _anchorCol, _caretLine, _caretCol)
            : (_caretLine, _caretCol, _anchorLine, _anchorCol);
    }

    public void SelectAll()
    {
        if (LineCount == 0)
        {
            return;
        }

        int last = LineCount - 1;
        _anchorLine = 0;
        _anchorCol = 0;
        _caretLine = last;
        _caretCol = ContentLength(last);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private void MoveCaret(int line, int col, bool extend)
    {
        line = Math.Clamp(line, 0, Math.Max(0, LineCount - 1));
        RevealLine(line); // never leave the caret trapped inside a collapsed block
        col = Math.Clamp(col, 0, ContentLength(line));
        _caretLine = line;
        _caretCol = col;
        if (!extend)
        {
            _anchorLine = line;
            _anchorCol = col;
        }

        EnsureCaretVisible();
        ResetCaretBlink();
        UpdateBracketMatch();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    // ----- caret blink ----------------------------------------------------

    private void ResetCaretBlink()
    {
        _caretOn = true;
        if (IsEditable && Focused)
        {
            _caretTimer.Stop();
            _caretTimer.Start();
        }
    }

    private void InvalidateCaretRegion()
    {
        int row = RowOf(_caretLine);
        if (row < 0)
        {
            return;
        }

        int x = TextLeft + Math.Min(_caretCol, ContentLength(_caretLine)) * _charWidth - _hOffset;
        Invalidate(new Rectangle(x - 1, row * _lineHeight, 3, _lineHeight + 1));
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        ResetCaretBlink();
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _caretTimer.Stop();
        Invalidate();
    }

    private void EnsureCaretVisible()
    {
        int caretDisplay = DisplayIndexOf(_caretLine);
        int topDisplay = DisplayIndexOf(_topLine);
        if (caretDisplay < topDisplay)
        {
            ScrollToLine(_caretLine);
        }
        else if (caretDisplay >= topDisplay + VisibleLines)
        {
            ScrollToLine(LineAtDisplay(caretDisplay - VisibleLines + 1));
        }

        // Horizontal: keep the caret column within the visible band.
        int cols = Math.Max(1, ContentWidth / Math.Max(1, _charWidth));
        int leftCol = FirstVisibleColumn;
        if (_caretCol < leftCol)
        {
            SetHorizontalOffset(_caretCol * _charWidth);
        }
        else if (_caretCol >= leftCol + cols)
        {
            SetHorizontalOffset((_caretCol - cols + 1) * _charWidth);
        }
    }

    private void SetHorizontalOffset(int pixels)
    {
        int max = Math.Max(0, _hScroll.Maximum - _hScroll.LargeChange + 1);
        _hOffset = Math.Clamp(pixels, 0, max);
        if (_hScroll.Visible)
        {
            _hScroll.Value = Math.Clamp(_hOffset, _hScroll.Minimum, _hScroll.Maximum);
        }
    }

    // ----- editing --------------------------------------------------------

    private int CaretPosition()
    {
        int line = Math.Clamp(_caretLine, 0, Math.Max(0, LineCount - 1));
        int col = Math.Min(_caretCol, ContentLength(line));
        return _table!.LineStart(line) + col;
    }

    // ----- bracket matching ----------------------------------------------

    private int TotalChars => _table?.Length ?? (int)(_document?.CharLength ?? 0);

    private string ReadRange(int position, int count)
    {
        if (position < 0 || count <= 0)
        {
            return string.Empty;
        }

        return _table is not null ? _table.GetText(position, count) : _document?.GetTextByChars(position, count) ?? string.Empty;
    }

    private (int Line, int Column) PositionToLineColumn(int position)
    {
        if (_table is not null)
        {
            int line = _table.LineFromPosition(position);
            return (line, position - _table.LineStart(line));
        }

        int docLine = _document?.LineFromChar(position) ?? 0;
        return (docLine, position - (_document?.GetLineCharStart(docLine) ?? 0));
    }

    private static char MatchingOpen(char c) => c switch { ')' => '(', ']' => '[', '}' => '{', _ => '\0' };
    private static char MatchingClose(char c) => c switch { '(' => ')', '[' => ']', '{' => '}', _ => '\0' };

    /// <summary>Recompute the matched-bracket pair for the current caret (editable mode only).</summary>
    private void UpdateBracketMatch()
    {
        _bracketMatch = null;
        if (_table is null)
        {
            return;
        }

        int caret = CaretPosition();
        foreach (int pos in stackalloc[] { caret, caret - 1 })
        {
            if (pos < 0 || pos >= TotalChars)
            {
                continue;
            }

            char c = ReadRange(pos, 1)[0];
            if (MatchingClose(c) != '\0')
            {
                int match = ScanForBracket(pos, c, MatchingClose(c), forward: true);
                if (match >= 0) { _bracketMatch = (pos, match); return; }
            }
            else if (MatchingOpen(c) != '\0')
            {
                int match = ScanForBracket(pos, c, MatchingOpen(c), forward: false);
                if (match >= 0) { _bracketMatch = (match, pos); return; }
            }
        }
    }

    /// <summary>Scan from <paramref name="start"/> for the bracket that balances it, up to a cap.</summary>
    private int ScanForBracket(int start, char self, char target, bool forward)
    {
        const int chunk = 8192;
        int depth = 1;
        int scanned = 0;

        if (forward)
        {
            int i = start + 1;
            while (i < TotalChars && scanned < MaxBracketScan)
            {
                int take = Math.Min(chunk, TotalChars - i);
                string buffer = ReadRange(i, take);
                for (int k = 0; k < buffer.Length; k++)
                {
                    char ch = buffer[k];
                    if (ch == self) depth++;
                    else if (ch == target && --depth == 0) return i + k;
                }

                i += take;
                scanned += take;
            }
        }
        else
        {
            int i = start - 1;
            while (i >= 0 && scanned < MaxBracketScan)
            {
                int take = Math.Min(chunk, i + 1);
                int from = i - take + 1;
                string buffer = ReadRange(from, take);
                for (int k = buffer.Length - 1; k >= 0; k--)
                {
                    char ch = buffer[k];
                    if (ch == self) depth++;
                    else if (ch == target && --depth == 0) return from + k;
                }

                i -= take;
                scanned += take;
            }
        }

        return -1;
    }

    public void InsertText(string text)
    {
        if (_table is null || text.Length == 0)
        {
            return;
        }

        DeleteSelectionInternal();
        int pos = CaretPosition();
        _table.Insert(pos, text);
        SetCaretToPosition(pos + text.Length);
        OnEdited();
    }

    /// <summary>Enter with auto-indent: the new line copies the current line's leading whitespace.</summary>
    private void InsertNewLineWithIndent()
    {
        if (_table is null)
        {
            return;
        }

        string head = GetSlice(_caretLine, 0, Math.Min(ContentLength(_caretLine), 256));
        int ws = 0;
        while (ws < head.Length && (head[ws] == ' ' || head[ws] == '\t'))
        {
            ws++;
        }

        int indentLen = Math.Min(ws, _caretCol); // don't add indent from beyond the caret
        InsertText("\r\n" + head[..indentLen]);
    }

    private void DeleteBackward()
    {
        if (_table is null)
        {
            return;
        }

        if (HasSelection)
        {
            DeleteSelectionInternal();
            OnEdited();
            return;
        }

        int pos = CaretPosition();
        if (pos <= 0)
        {
            return;
        }

        // Remove a CRLF pair as a single step.
        int remove = (pos >= 2 && _table.GetText(pos - 2, 2) == "\r\n") ? 2 : 1;
        _table.Delete(pos - remove, remove);
        SetCaretToPosition(pos - remove);
        OnEdited();
    }

    private void DeleteForward()
    {
        if (_table is null)
        {
            return;
        }

        if (HasSelection)
        {
            DeleteSelectionInternal();
            OnEdited();
            return;
        }

        int pos = CaretPosition();
        if (pos >= _table.Length)
        {
            return;
        }

        int remove = (_table.GetText(pos, 2) == "\r\n") ? 2 : 1;
        _table.Delete(pos, remove);
        SetCaretToPosition(pos);
        OnEdited();
    }

    private void DeleteSelectionInternal()
    {
        if (_table is null || !HasSelection)
        {
            return;
        }

        (int sl, int sc, int el, int ec) = NormalizedSelection();
        int start = _table.LineStart(sl) + Math.Min(sc, ContentLength(sl));
        int end = _table.LineStart(el) + Math.Min(ec, ContentLength(el));
        if (end > start)
        {
            _table.Delete(start, end - start);
        }

        SetCaretToPosition(start);
    }

    private void CutSelection()
    {
        if (_table is null || !HasSelection)
        {
            return;
        }

        CopySelection();
        DeleteSelectionInternal();
        OnEdited();
    }

    private void PasteAtCaret()
    {
        if (_table is null || !Clipboard.ContainsText())
        {
            return;
        }

        InsertText(Clipboard.GetText());
    }

    public void Undo()
    {
        if (_table is null)
        {
            return;
        }

        int caret = _table.Undo();
        if (caret >= 0)
        {
            SetCaretToPosition(caret);
            OnEdited();
        }
    }

    private void Redo()
    {
        if (_table is null)
        {
            return;
        }

        int caret = _table.Redo();
        if (caret >= 0)
        {
            SetCaretToPosition(caret);
            OnEdited();
        }
    }

    private void SetCaretToPosition(int position)
    {
        position = Math.Clamp(position, 0, _table!.Length);
        _caretLine = _table.LineFromPosition(position);
        _caretCol = position - _table.LineStart(_caretLine);
        _anchorLine = _caretLine;
        _anchorCol = _caretCol;
    }

    private void OnEdited()
    {
        _dirty = true;
        _maxLineChars = Math.Max(_maxLineChars, ContentLength(_caretLine));
        UpdateScrollRanges();
        EnsureCaretVisible();
        ResetCaretBlink();
        UpdateBracketMatch();
        ModifiedChanged?.Invoke(this, EventArgs.Empty);
        TextChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    // ----- scrolling ------------------------------------------------------

    private void ScrollToLine(int line)
    {
        _topLine = Math.Clamp(line, 0, Math.Max(0, LineCount - 1));
        NormalizeTop();
        if (_vScroll.Visible)
        {
            _vScroll.Value = Math.Clamp(DisplayIndexOf(_topLine), 0, _vScroll.Maximum);
        }

        Invalidate();
    }

    private void CopySelection()
    {
        if (!HasSelection)
        {
            return;
        }

        (int sl, int sc, int el, int ec) = NormalizedSelection();
        var builder = new StringBuilder();

        for (int line = sl; line <= el; line++)
        {
            int content = ContentLength(line);
            int from = line == sl ? Math.Min(sc, content) : 0;
            int to = line == el ? Math.Min(ec, content) : content;
            if (to > from)
            {
                // Read only the selected span, capped so a huge selection can't be materialised.
                int budget = MaxCopyChars - builder.Length + 1;
                builder.Append(GetSlice(line, from, Math.Min(to - from, budget)));
            }

            if (line != el)
            {
                builder.Append("\r\n");
            }

            if (builder.Length > MaxCopyChars)
            {
                MessageBox.Show(this, $"The selection is too large to copy (over {MaxCopyChars / 1_000_000} million characters).",
                    "PurePad", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }

        if (builder.Length > 0)
        {
            Clipboard.SetText(builder.ToString());
        }
    }

    // ----- painting -------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(_background);
        g.TextRenderingHint = TextHint;
        if (LineCount == 0)
        {
            return;
        }

        (int sl, int sc, int el, int ec) = NormalizedSelection();
        bool hasSelection = HasSelection;
        var contentClip = new Rectangle(TextLeft, 0, ContentWidth, Height);
        int rows = VisibleLines + 1;
        int newMaxChars = _maxLineChars;

        int lineIndex = _topLine;
        for (int row = 0; row < rows && lineIndex < LineCount; row++, lineIndex = NextVisible(lineIndex))
        {
            int y = row * _lineHeight;
            int startCol = FirstVisibleColumn;
            int content = ContentLength(lineIndex);
            newMaxChars = Math.Max(newMaxChars, content);

            // Read only the horizontally visible characters — a 100 MB line costs nothing extra.
            string slice = GetSlice(lineIndex, startCol, VisibleColumns);
            int xOrigin = TextLeft + startCol * _charWidth - _hOffset;

            // Current-line highlight (behind selection/text), full content width.
            if (IsEditable && lineIndex == _caretLine)
            {
                using var lineBrush = new SolidBrush(_currentLineFill);
                g.FillRectangle(lineBrush, 0, y, Width, _lineHeight);
            }

            g.SetClip(contentClip);

            if (hasSelection && lineIndex >= sl && lineIndex <= el)
            {
                int from = lineIndex == sl ? sc : 0;
                int to = lineIndex == el ? ec : content + 1;
                DrawSelection(g, y, Math.Max(0, from), Math.Max(from, to));
            }

            if (_highlighter is not null && _syntaxTheme is not null && slice.Length > 0)
            {
                try
                {
                    DrawColouredSlice(g, lineIndex, startCol, content, slice, xOrigin, y);
                }
                catch (Exception ex)
                {
                    // A highlighter bug must never blank the whole editor: fall back to plain text.
                    DrawGlyphs(g, slice, xOrigin, y, _foreground);
                    LogRenderFault(ex);
                }
            }
            else
            {
                DrawGlyphs(g, slice, xOrigin, y, _foreground);
            }

            // Marker that a collapsed block follows this (visible) head line.
            if (FoldStartingAt(lineIndex) >= 0)
            {
                int ex = TextLeft + (content + 1) * _charWidth - _hOffset;
                var chip = new Rectangle(ex, y + 2, _charWidth * 3, _lineHeight - 4);
                using var chipPen = new Pen(_gutterForeground);
                g.DrawRectangle(chipPen, chip);
                TextRenderer.DrawText(g, "…", Font, chip, _gutterForeground,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            g.ResetClip();
        }

        // Overlays are guarded so an overlay bug can never blank the editor to the WinForms
        // red-X placeholder — the text is already painted; a fault just skips the decorations.
        try
        {
            DrawBracketGuide(g, contentClip);
            DrawBracketMatch(g, contentClip);
            DrawCaret(g);
            DrawGutter(g);
            DrawStickyHeaders(g);
        }
        catch (Exception ex)
        {
            LogRenderFault(ex);
        }

        if (newMaxChars != _maxLineChars)
        {
            _maxLineChars = newMaxChars;
            UpdateScrollRanges();
        }
    }

    /// <summary>Outline the matched bracket pair, when the caret sits beside one.</summary>
    private void DrawBracketMatch(Graphics g, Rectangle contentClip)
    {
        if (_bracketMatch is not { } pair)
        {
            return;
        }

        g.SetClip(contentClip);
        using var fill = new SolidBrush(Color.FromArgb(70, _bracketColor));
        using var pen = new Pen(_bracketColor);
        DrawBracketBox(g, fill, pen, pair.Open);
        DrawBracketBox(g, fill, pen, pair.Close);
        g.ResetClip();
    }

    private void DrawBracketBox(Graphics g, Brush fill, Pen pen, int charPosition)
    {
        (int line, int col) = PositionToLineColumn(charPosition);
        int row = RowOf(line);
        if (row < 0)
        {
            return;
        }

        int x = TextLeft + col * _charWidth - _hOffset;
        if (x + _charWidth < TextLeft || x > Width)
        {
            return;
        }

        int y = row * _lineHeight;
        int w = Math.Max(2, _charWidth);
        g.FillRectangle(fill, x, y, w, _lineHeight - 1);
        g.DrawRectangle(pen, x, y, w, _lineHeight - 1);
    }

    private void DrawCaret(Graphics g)
    {
        int row = RowOf(_caretLine);
        if (!IsEditable || !Focused || !_caretOn || row < 0)
        {
            return;
        }

        int col = Math.Min(_caretCol, ContentLength(_caretLine));
        int x = TextLeft + col * _charWidth - _hOffset;
        if (x < TextLeft || x > Width)
        {
            return;
        }

        int y = row * _lineHeight;
        using var pen = new Pen(_foreground);
        g.DrawLine(pen, x, y, x, y + _lineHeight);
    }

    // Tokenize from the line start (bounded) so horizontally-scrolled slices know their context.
    private const int TokenizeCap = 4000;

    /// <summary>
    /// Colour the visible slice by tokenizing the line from its start (capped at
    /// <see cref="TokenizeCap"/>). A token cut off at the cap — e.g. a multi-megabyte string —
    /// is extended across the rest of the line, so a giant string stays coloured even when the
    /// view is scrolled far into it (and we never tokenize the whole huge line).
    /// </summary>
    private void DrawColouredSlice(Graphics g, int lineIndex, int startCol, int content, string slice, int xOrigin, int y)
    {
        int headLen = Math.Min(content, TokenizeCap);
        string head = startCol == 0 && slice.Length >= headLen ? slice : GetSlice(lineIndex, 0, headLen);
        var tokens = _highlighter!.Tokenize(head);
        TokenKind trailing = TrailingKind(tokens, headLen, content);

        // Resolve a colour for every visible column, then draw contiguous same-colour runs.
        int len = slice.Length;
        var kinds = new TokenKind[len];
        for (int i = 0; i < len; i++)
        {
            kinds[i] = startCol + i >= headLen ? trailing : TokenKind.PlainText;
        }

        foreach (TextToken token in tokens)
        {
            int a = Math.Max(token.Start, startCol);
            int b = Math.Min(token.End, startCol + len);
            for (int c = a; c < b; c++)
            {
                kinds[c - startCol] = token.Kind;
            }
        }

        int runStart = 0;
        for (int i = 1; i <= len; i++)
        {
            if (i == len || kinds[i] != kinds[runStart])
            {
                Color color = kinds[runStart] == TokenKind.PlainText ? _foreground : _syntaxTheme!.ColorFor(kinds[runStart]);
                DrawRun(g, slice, runStart, i, xOrigin, y, color);
                runStart = i;
            }
        }
    }

    /// <summary>Kind of the token cut off at the cap (extended across the rest of a huge line), else plain.</summary>
    private static TokenKind TrailingKind(IEnumerable<TextToken> tokens, int headLen, int content)
    {
        if (content <= headLen)
        {
            return TokenKind.PlainText;
        }

        TokenKind kind = TokenKind.PlainText;
        foreach (TextToken token in tokens)
        {
            if (token.Start <= headLen - 1 && token.End >= headLen)
            {
                kind = token.Kind;
            }
        }

        return kind;
    }

    private void DrawRun(Graphics g, string slice, int from, int to, int xOrigin, int y, Color color)
    {
        if (to <= from)
        {
            return;
        }

        string run = slice.Substring(from, to - from);
        int x = xOrigin + from * _charWidth;
        DrawGlyphs(g, run, x, y, color);
    }

    private bool _renderFaultLogged;

    /// <summary>Record the first per-line render fault to the error log (once per session).</summary>
    private void LogRenderFault(Exception ex)
    {
        if (_renderFaultLogged)
        {
            return;
        }

        _renderFaultLogged = true;
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "purepad-error.log");
            File.AppendAllText(path, $"{DateTime.Now:o} render fault: {ex}\n\n");
        }
        catch
        {
            // logging must never itself throw during a paint
        }
    }

    /// <summary>Draw a run of monospace text with the current antialiasing hint, on the character grid.</summary>
    private void DrawGlyphs(Graphics g, string text, int x, int y, Color color)
    {
        if (text.Length == 0)
        {
            return;
        }

        using var brush = new SolidBrush(color);
        g.DrawString(text, Font, brush, x, y, GlyphFormat);
    }

    private void DrawSelection(Graphics g, int y, int fromCol, int toCol)
    {
        int x1 = TextLeft + fromCol * _charWidth - _hOffset;
        int x2 = TextLeft + toCol * _charWidth - _hOffset;
        using var brush = new SolidBrush(_selection);
        g.FillRectangle(brush, x1, y, Math.Max(1, x2 - x1), _lineHeight);
    }

    private void DrawGutter(Graphics g)
    {
        using var gutterBrush = new SolidBrush(_gutterBackground);
        g.FillRectangle(gutterBrush, 0, 0, _gutterWidth, Height);
        using var borderPen = new Pen(ControlPaint.Dark(_gutterBackground, 0.05f));
        g.DrawLine(borderPen, _gutterWidth - 1, 0, _gutterWidth - 1, Height);

        int rows = VisibleLines + 1;
        int numbersRight = _gutterWidth - FoldMarginWidth;
        int lineIndex = _topLine;
        for (int row = 0; row < rows && lineIndex < LineCount; row++, lineIndex = NextVisible(lineIndex))
        {
            string number = (lineIndex + 1).ToString();
            int numberWidth = TextRenderer.MeasureText(number, Font).Width;
            TextRenderer.DrawText(g, number, Font, new Point(numbersRight - GutterPadding - numberWidth, row * _lineHeight), _gutterForeground);
            DrawFoldMarker(g, row, lineIndex);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _caretTimer.Dispose();
            _dragScrollTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
