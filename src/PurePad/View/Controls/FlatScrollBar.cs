using System.Drawing;
using System.Windows.Forms;

namespace PurePad.View.Controls;

/// <summary>
/// A thin, flat, theme-coloured scroll bar that replaces the native <see cref="ScrollBar"/> (which
/// paints white and ignores <c>BackColor</c> in dark mode). It exposes the same
/// Minimum/Maximum/Value/LargeChange/SmallChange surface and a <see cref="Scroll"/> event, and
/// raises <see cref="Scroll"/> only on user interaction (never when <see cref="Value"/> is set in
/// code), so a host can drive it without feedback loops.
/// </summary>
internal sealed class FlatScrollBar : Control
{
    private const int MinThumb = 24;

    private readonly bool _vertical;
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private int _largeChange = 10;
    private int _smallChange = 1;

    private bool _dragging;
    private int _dragOffset;
    private bool _hot;

    private Color _track = SystemColors.Control;
    private Color _thumb = Color.FromArgb(190, 190, 190);
    private Color _thumbHot = Color.FromArgb(160, 160, 160);

    public FlatScrollBar(bool vertical)
    {
        _vertical = vertical;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Dock = vertical ? DockStyle.Right : DockStyle.Bottom;
        if (vertical) { Width = 14; } else { Height = 14; }
    }

    /// <summary>Raised when the user changes <see cref="Value"/> (drag or track click), not on code writes.</summary>
    public event EventHandler? Scroll;

    public int Minimum
    {
        get => _minimum;
        set { _minimum = value; ClampValue(); Invalidate(); }
    }

    public int Maximum
    {
        get => _maximum;
        set { _maximum = value; ClampValue(); Invalidate(); }
    }

    public int LargeChange
    {
        get => _largeChange;
        set { _largeChange = Math.Max(1, value); ClampValue(); Invalidate(); }
    }

    public int SmallChange
    {
        get => _smallChange;
        set => _smallChange = Math.Max(1, value);
    }

    /// <summary>Current position. Setting this does NOT raise <see cref="Scroll"/> (matches native behaviour).</summary>
    public int Value
    {
        get => _value;
        set { _value = Clamp(value); Invalidate(); }
    }

    /// <summary>Largest value the thumb can reach (Maximum minus one page).</summary>
    private int ValueMax => Math.Max(_minimum, _maximum - _largeChange + 1);

    public void SetColors(Color track, Color thumb, Color thumbHot)
    {
        _track = track;
        _thumb = thumb;
        _thumbHot = thumbHot;
        Invalidate();
    }

    private int Clamp(int v) => Math.Clamp(v, _minimum, ValueMax);

    private void ClampValue() => _value = Clamp(_value);

    // ----- geometry -------------------------------------------------------

    private int TrackLength => _vertical ? Height : Width;

    private int ThumbLength
    {
        get
        {
            int total = Math.Max(1, _maximum - _minimum + 1);
            double frac = Math.Min(1.0, (double)_largeChange / total);
            return Math.Max(MinThumb, (int)(TrackLength * frac));
        }
    }

    private int ThumbOffset
    {
        get
        {
            int span = TrackLength - ThumbLength;
            int range = ValueMax - _minimum;
            if (span <= 0 || range <= 0)
            {
                return 0;
            }

            return (int)((double)(_value - _minimum) / range * span);
        }
    }

    private Rectangle ThumbRect => _vertical
        ? new Rectangle(2, ThumbOffset, Width - 4, ThumbLength)
        : new Rectangle(ThumbOffset, 2, ThumbLength, Height - 4);

    // ----- painting -------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        using (var track = new SolidBrush(_track))
        {
            g.FillRectangle(track, ClientRectangle);
        }

        Rectangle r = ThumbRect;
        if ((_vertical ? r.Height : r.Width) >= TrackLength)
        {
            return; // nothing to scroll — hide the thumb
        }

        using var thumb = new SolidBrush(_dragging || _hot ? _thumbHot : _thumb);
        g.FillRectangle(thumb, r);
    }

    // ----- interaction ----------------------------------------------------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int pos = _vertical ? e.Y : e.X;
        Rectangle r = ThumbRect;
        int thumbStart = _vertical ? r.Y : r.X;
        int thumbEnd = thumbStart + (_vertical ? r.Height : r.Width);

        if (pos >= thumbStart && pos < thumbEnd)
        {
            _dragging = true;
            _dragOffset = pos - thumbStart;
            Capture = true;
        }
        else
        {
            // Page toward the click.
            SetUserValue(_value + (pos < thumbStart ? -_largeChange : _largeChange));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            int pos = (_vertical ? e.Y : e.X) - _dragOffset;
            int span = TrackLength - ThumbLength;
            int range = ValueMax - _minimum;
            if (span > 0 && range > 0)
            {
                SetUserValue(_minimum + (int)Math.Round((double)pos / span * range));
            }
        }
        else
        {
            bool hot = ThumbRect.Contains(e.Location);
            if (hot != _hot) { _hot = hot; Invalidate(); }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hot) { _hot = false; Invalidate(); }
    }

    private void SetUserValue(int v)
    {
        int clamped = Clamp(v);
        if (clamped != _value)
        {
            _value = clamped;
            Invalidate();
            Scroll?.Invoke(this, EventArgs.Empty);
        }
    }
}
