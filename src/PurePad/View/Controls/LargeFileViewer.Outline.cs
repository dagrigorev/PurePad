using System.Drawing;
using System.Windows.Forms;

namespace PurePad.View.Controls;

/// <summary>
/// Editor "code map" overlays: VS-style sticky scroll (the enclosing block headers pinned at the
/// top of the view) and a vertical scope guide connecting a matched bracket pair. Both are derived
/// purely from indentation, so they work for any file type without a language-specific parser.
/// </summary>
public sealed partial class LargeFileViewer
{
    private const int StickyReadCap = 256;   // leading chars read to measure a line's indent
    private const int MaxStickyRows = 6;      // never cover more than this many rows with headers
    private const int MaxStickyScan = 20_000; // give up walking upward beyond this many lines

    private bool _stickyEnabled = true;
    private bool _isDark;
    private Color _stickyBackground = Color.FromArgb(246, 246, 246);
    private Color _stickyBorder = Color.FromArgb(210, 210, 210);
    private Color _guideColor = Color.FromArgb(120, 51, 153, 255);

    // Header line index + its on-screen top Y, recorded each paint for click-to-jump.
    private readonly List<(int Line, int Top)> _stickyHits = new();

    /// <summary>Enable or disable the sticky-scroll header overlay.</summary>
    public bool StickyScrollEnabled
    {
        get => _stickyEnabled;
        set { if (_stickyEnabled != value) { _stickyEnabled = value; Invalidate(); } }
    }

    /// <summary>Refresh derived overlay colours from the current palette (called by ApplyColors).</summary>
    private void RefreshOutlineColors()
    {
        _isDark = _background.GetBrightness() < 0.5f;
        _stickyBackground = _isDark ? ControlPaint.Light(_background, 0.06f) : ControlPaint.Dark(_background, 0.03f);
        _stickyBorder = _isDark ? ControlPaint.Light(_background, 0.20f) : ControlPaint.Dark(_background, 0.12f);
        _guideColor = Color.FromArgb(_isDark ? 90 : 70, _bracketColor);
    }

    // ----- indentation ----------------------------------------------------

    /// <summary>Visual indent (in columns, tabs expanded to 4) of a line, or -1 when it is blank.</summary>
    private int IndentColumns(int line)
    {
        string s = GetSlice(line, 0, StickyReadCap);
        int col = 0;
        foreach (char c in s)
        {
            if (c == ' ') col++;
            else if (c == '\t') col += 4 - (col % 4);
            else return col; // first non-whitespace char
        }

        return -1; // only whitespace within the cap => treat as blank
    }

    /// <summary>
    /// The chain of enclosing "header" lines above <paramref name="firstVisible"/>: each successive
    /// line with strictly smaller indentation, walking upward. Returned outermost-first.
    /// </summary>
    private List<int> ComputeStickyHeaders(int firstVisible)
    {
        var headers = new List<int>();
        if (firstVisible <= 0)
        {
            return headers;
        }

        // Threshold starts at the first visible non-blank line's indent.
        int threshold = int.MaxValue;
        for (int probe = firstVisible; probe < LineCount && probe < firstVisible + 4; probe++)
        {
            int ind = IndentColumns(probe);
            if (ind >= 0) { threshold = ind; break; }
        }

        int limit = Math.Max(0, firstVisible - MaxStickyScan);
        for (int line = firstVisible - 1; line >= limit && headers.Count < MaxStickyRows; line--)
        {
            int ind = IndentColumns(line);
            if (ind < 0 || ind >= threshold)
            {
                continue;
            }

            headers.Add(HeaderLineFor(line));
            threshold = ind;
            if (ind == 0)
            {
                break;
            }
        }

        headers.Reverse();
        return headers;
    }

    /// <summary>
    /// The line to actually display as a header. For Allman brace style the enclosing line is a
    /// lone "{"; the meaningful declaration is the non-blank line just above it, so use that.
    /// </summary>
    private int HeaderLineFor(int line)
    {
        string trimmed = GetSlice(line, 0, StickyReadCap).Trim();
        if (trimmed.Length == 0 || trimmed[0] != '{')
        {
            return line;
        }

        for (int up = line - 1; up >= Math.Max(0, line - 3); up--)
        {
            if (GetSlice(up, 0, StickyReadCap).Trim().Length > 0)
            {
                return up;
            }
        }

        return line;
    }

    // ----- painting -------------------------------------------------------

    /// <summary>Draw the pinned enclosing-block headers over the top of the view.</summary>
    private void DrawStickyHeaders(Graphics g)
    {
        _stickyHits.Clear();
        if (!_stickyEnabled || LineCount == 0)
        {
            return;
        }

        List<int> headers = ComputeStickyHeaders(_topLine);
        int max = Math.Min(headers.Count, Math.Max(0, VisibleLines / 2));
        if (max <= 0)
        {
            return;
        }

        for (int i = 0; i < max; i++)
        {
            int line = headers[i];
            int y = i * _lineHeight;

            using (var bg = new SolidBrush(_stickyBackground))
            {
                g.FillRectangle(bg, 0, y, Width, _lineHeight);
            }

            // Gutter number for the header line.
            string number = (line + 1).ToString();
            int numberWidth = TextRenderer.MeasureText(number, Font).Width;
            TextRenderer.DrawText(g, number, Font, new Point(_gutterWidth - GutterPadding - numberWidth, y), _gutterForeground);

            // Header text, pinned to the left (never horizontally scrolled) so it stays readable.
            int content = ContentLength(line);
            string slice = GetSlice(line, 0, VisibleColumns);
            var clip = new Rectangle(TextLeft, y, ContentWidth, _lineHeight);
            g.SetClip(clip);
            if (_highlighter is not null && _syntaxTheme is not null && slice.Length > 0)
            {
                DrawColouredSlice(g, line, 0, content, slice, TextLeft, y);
            }
            else
            {
                TextRenderer.DrawText(g, slice, Font, new Point(TextLeft, y), _foreground,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }

            g.ResetClip();
            _stickyHits.Add((line, y));
        }

        // Separator under the pinned region.
        int sepY = max * _lineHeight;
        using var pen = new Pen(_stickyBorder);
        g.DrawLine(pen, 0, sepY, Width, sepY);
    }

    /// <summary>Vertical scope line connecting a matched bracket pair spanning multiple lines.</summary>
    private void DrawBracketGuide(Graphics g, Rectangle contentClip)
    {
        if (_bracketMatch is not { } pair)
        {
            return;
        }

        (int openLine, int openCol) = PositionToLineColumn(pair.Open);
        (int closeLine, _) = PositionToLineColumn(pair.Close);
        if (closeLine <= openLine || IsHidden(openLine) || IsHidden(closeLine))
        {
            return; // same line, or the block is collapsed
        }

        int x = TextLeft + openCol * _charWidth - _hOffset + _charWidth / 2;
        int yTop = (DisplayIndexOf(openLine + 1) - DisplayIndexOf(_topLine)) * _lineHeight;
        int yBottom = (DisplayIndexOf(closeLine) - DisplayIndexOf(_topLine)) * _lineHeight;

        g.SetClip(contentClip);
        using var pen = new Pen(_guideColor);
        g.DrawLine(pen, x, Math.Max(0, yTop), x, Math.Min(Height, yBottom));
        g.ResetClip();
    }

    /// <summary>If a click lands on a pinned header, jump to that line. Returns true when handled.</summary>
    private bool TryStickyClick(Point location)
    {
        if (!_stickyEnabled || location.Y >= _stickyHits.Count * _lineHeight)
        {
            return false;
        }

        foreach ((int line, int top) in _stickyHits)
        {
            if (location.Y >= top && location.Y < top + _lineHeight)
            {
                ScrollToLine(line);
                return true;
            }
        }

        return false;
    }
}
