using System.Drawing;
using System.Windows.Forms;

namespace PurePad.View.Controls;

/// <summary>
/// VS-style code folding (outlining). Foldable regions are found from indentation — a line whose
/// following lines are more indented — so it works for any file type. Collapsed regions hide their
/// body lines; a display-row ↔ logical-line mapping lets the rest of the viewer keep working in
/// logical-line space while only visible lines are painted and counted for scrolling.
/// </summary>
public sealed partial class LargeFileViewer
{
    private const int MaxFoldScan = 500_000; // cap the downward scan when measuring a block's end

    private readonly List<(int Start, int End)> _folds = new(); // collapsed regions, sorted, non-overlapping
    private bool _foldingEnabled = true;

    /// <summary>Width of the gutter's fold margin (a square that scales with the font).</summary>
    private int FoldMarginWidth => _foldingEnabled ? _lineHeight : 0;

    // ----- region mapping -------------------------------------------------

    /// <summary>Index of the collapsed fold that hides <paramref name="line"/> (Start &lt; line ≤ End), or -1.</summary>
    private int FoldContaining(int line)
    {
        for (int i = 0; i < _folds.Count; i++)
        {
            if (_folds[i].Start < line && line <= _folds[i].End)
            {
                return i;
            }
        }

        return -1;
    }

    private int FoldStartingAt(int line)
    {
        for (int i = 0; i < _folds.Count; i++)
        {
            if (_folds[i].Start == line)
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsHidden(int line) => FoldContaining(line) >= 0;

    /// <summary>The first visible line strictly after <paramref name="line"/>.</summary>
    private int NextVisible(int line)
    {
        int n = line + 1;
        while (true)
        {
            int f = FoldContaining(n);
            if (f < 0)
            {
                return n;
            }

            n = _folds[f].End + 1;
        }
    }

    /// <summary>The first visible line strictly before <paramref name="line"/> (or -1).</summary>
    private int PrevVisible(int line)
    {
        int p = line - 1;
        while (p >= 0)
        {
            int f = FoldContaining(p);
            if (f < 0)
            {
                return p;
            }

            p = _folds[f].Start;
        }

        return -1;
    }

    private int TotalHidden
    {
        get
        {
            int sum = 0;
            foreach ((int start, int end) in _folds)
            {
                sum += end - start;
            }

            return sum;
        }
    }

    private int VisibleLineTotal => Math.Max(1, LineCount - TotalHidden);

    /// <summary>Count of visible lines before <paramref name="line"/> (its scrollbar position).</summary>
    private int DisplayIndexOf(int line)
    {
        int hidden = 0;
        foreach ((int start, int end) in _folds)
        {
            if (start + 1 <= line - 1) // hidden block [start+1, end] intersecting [0, line-1]
            {
                hidden += Math.Min(end, line - 1) - start;
            }
        }

        return line - hidden;
    }

    /// <summary>The logical line shown at display index <paramref name="display"/>.</summary>
    private int LineAtDisplay(int display)
    {
        int line = 0, disp = 0;
        foreach ((int start, int end) in _folds)
        {
            int gap = start - line + 1; // visible lines [line..start]
            if (disp + gap > display)
            {
                return line + (display - disp);
            }

            disp += gap;
            line = end + 1;
        }

        return Math.Clamp(line + (display - disp), 0, Math.Max(0, LineCount - 1));
    }

    /// <summary>Logical line at on-screen <paramref name="row"/> (0 = top), skipping folded lines.</summary>
    private int LineAtRow(int row)
    {
        int line = _topLine;
        for (int i = 0; i < row && line < LineCount; i++)
        {
            line = NextVisible(line);
        }

        return Math.Min(line, Math.Max(0, LineCount - 1));
    }

    /// <summary>On-screen row of <paramref name="line"/>, or -1 when it is above/below the view or hidden.</summary>
    private int RowOf(int line)
    {
        if (line < _topLine || IsHidden(line))
        {
            return -1;
        }

        int cur = _topLine, row = 0, limit = VisibleLines + 2;
        while (cur < line && row <= limit)
        {
            cur = NextVisible(cur);
            row++;
        }

        return cur == line && row <= limit ? row : -1;
    }

    /// <summary>Keep the top line on a visible line (e.g. after collapsing the block it sits in).</summary>
    private void NormalizeTop()
    {
        int f = FoldContaining(_topLine);
        if (f >= 0)
        {
            _topLine = _folds[f].Start;
        }
    }

    /// <summary>Scroll by <paramref name="delta"/> visible lines.</summary>
    private void ScrollVisibleLines(int delta)
    {
        int line = _topLine;
        if (delta > 0)
        {
            for (int i = 0; i < delta && line < LineCount - 1; i++) line = NextVisible(line);
        }
        else
        {
            for (int i = 0; i < -delta; i++) { int p = PrevVisible(line); if (p < 0) break; line = p; }
        }

        ScrollToLine(line);
    }

    // ----- detection ------------------------------------------------------

    /// <summary>True when <paramref name="line"/> opens a foldable block (its body is more indented).</summary>
    private bool IsFoldHead(int line)
    {
        int head = IndentColumns(line);
        if (head < 0)
        {
            return false;
        }

        for (int probe = line + 1, guard = 0; probe < LineCount && guard < 50; probe++, guard++)
        {
            int ind = IndentColumns(probe);
            if (ind >= 0)
            {
                return ind > head;
            }
        }

        return false;
    }

    /// <summary>Last line of the block opened at <paramref name="head"/> (trailing blanks excluded).</summary>
    private int ComputeFoldEnd(int head)
    {
        int h = IndentColumns(head);
        int end = head;
        int cap = Math.Min(LineCount, head + MaxFoldScan);
        for (int line = head + 1; line < cap; line++)
        {
            int ind = IndentColumns(line);
            if (ind < 0)
            {
                continue; // blank — may be interior; don't extend on it
            }

            if (ind > h)
            {
                end = line;
            }
            else
            {
                break;
            }
        }

        return end;
    }

    // ----- toggle ---------------------------------------------------------

    /// <summary>Collapse or expand the block whose head is <paramref name="head"/>.</summary>
    private void ToggleFold(int head)
    {
        if (!_foldingEnabled)
        {
            return;
        }

        int existing = FoldStartingAt(head);
        if (existing >= 0)
        {
            _folds.RemoveAt(existing);
        }
        else
        {
            if (!IsFoldHead(head))
            {
                return;
            }

            int end = ComputeFoldEnd(head);
            if (end <= head || _folds.Exists(f => head <= f.End && f.Start <= end))
            {
                return; // nothing to fold, or it would overlap an existing fold
            }

            _folds.Add((head, end));
            _folds.Sort((a, b) => a.Start.CompareTo(b.Start));

            if (IsHidden(_caretLine))
            {
                _caretLine = _anchorLine = head;
                _caretCol = _anchorCol = Math.Min(_caretCol, ContentLength(head));
            }
        }

        NormalizeTop();
        UpdateScrollRanges();
        Invalidate();
    }

    /// <summary>Collapse/expand the block containing the caret (Ctrl+M).</summary>
    private void ToggleFoldAtCaret()
    {
        // Prefer the caret's own line if it heads a block; otherwise the nearest enclosing head.
        if (FoldStartingAt(_caretLine) >= 0 || IsFoldHead(_caretLine))
        {
            ToggleFold(_caretLine);
            return;
        }

        int childIndent = IndentColumns(_caretLine);
        for (int line = _caretLine - 1; line >= 0 && _caretLine - line < MaxFoldScan; line--)
        {
            int ind = IndentColumns(line);
            if (ind >= 0 && (childIndent < 0 || ind < childIndent) && IsFoldHead(line))
            {
                ToggleFold(line);
                return;
            }
        }
    }

    /// <summary>Expand any collapsed fold that hides <paramref name="line"/> (so the caret can land there).</summary>
    private void RevealLine(int line)
    {
        int f = FoldContaining(line);
        if (f >= 0)
        {
            _folds.RemoveAt(f);
            UpdateScrollRanges();
        }
    }

    // ----- gutter markers -------------------------------------------------

    /// <summary>Draw the [+]/[-] fold marker for <paramref name="line"/> at screen <paramref name="row"/>.</summary>
    private void DrawFoldMarker(Graphics g, int row, int line)
    {
        if (!_foldingEnabled)
        {
            return;
        }

        bool collapsed = FoldStartingAt(line) >= 0;
        if (!collapsed && !IsFoldHead(line))
        {
            return;
        }

        int side = Math.Max(8, _lineHeight - 6);
        int left = _gutterWidth - FoldMarginWidth + (FoldMarginWidth - side) / 2;
        int top = row * _lineHeight + (_lineHeight - side) / 2;
        var box = new Rectangle(left, top, side, side);

        using var pen = new Pen(_gutterForeground);
        g.DrawRectangle(pen, box);
        int cy = box.Top + box.Height / 2;
        int cx = box.Left + box.Width / 2;
        int arm = side / 2 - 2;
        g.DrawLine(pen, cx - arm, cy, cx + arm, cy);   // minus (both states)
        if (collapsed)
        {
            g.DrawLine(pen, cx, cy - arm, cx, cy + arm); // plus (collapsed)
        }
    }

    /// <summary>Handle a click in the fold margin. Returns true when it toggled a fold.</summary>
    private bool TryFoldMarginClick(Point location)
    {
        if (!_foldingEnabled)
        {
            return false;
        }

        int marginLeft = _gutterWidth - FoldMarginWidth;
        if (location.X < marginLeft || location.X >= _gutterWidth)
        {
            return false;
        }

        int row = location.Y / _lineHeight;
        int line = LineAtRow(row);
        if (FoldStartingAt(line) >= 0 || IsFoldHead(line))
        {
            ToggleFold(line);
            return true;
        }

        return false;
    }

    /// <summary>Reset all folds (e.g. when a new document loads).</summary>
    private void ClearFolds() => _folds.Clear();
}
