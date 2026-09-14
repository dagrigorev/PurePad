using System.Drawing;
using System.Windows.Forms;
using PurePad.Diagnostics;
using PurePad.Theming;

namespace PurePad.View.Controls;

/// <summary>
/// A dockable panel that lists <see cref="Diagnostic"/>s from the syntax checker. Double-
/// clicking a row asks the host to jump to that location via <see cref="DiagnosticActivated"/>.
/// It is a passive view: it renders what it is given and reports clicks, nothing more.
/// </summary>
public sealed class DiagnosticsPanel : Panel
{
    private readonly ListView _list;
    private readonly Label _header;
    private IReadOnlyList<Diagnostic> _diagnostics = Array.Empty<Diagnostic>();

    private Color _headerBack = SystemColors.Control;
    private Color _headerFore = SystemColors.ControlText;

    public DiagnosticsPanel()
    {
        Dock = DockStyle.Bottom;
        Height = 140;

        _header = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Text = "Problems",
            Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold),
        };

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = System.Windows.Forms.View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            BorderStyle = BorderStyle.None,
            OwnerDraw = true, // owner-drawn so the column header can follow the theme
        };
        _list.Columns.Add("Severity", 80);
        _list.Columns.Add("Line", 50);
        _list.Columns.Add("Col", 50);
        _list.Columns.Add("Message", 640);
        _list.DoubleClick += OnRowActivated;
        _list.DrawColumnHeader += OnDrawColumnHeader;
        _list.DrawItem += (s, e) => e.DrawDefault = true;
        _list.DrawSubItem += (s, e) => e.DrawDefault = true;
        _list.Resize += (s, e) => StretchLastColumn();

        Controls.Add(_list);
        Controls.Add(_header);
    }

    /// <summary>Raised when the user activates (double-clicks) a diagnostic row.</summary>
    public event EventHandler<Diagnostic>? DiagnosticActivated;

    public void SetDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        _diagnostics = diagnostics ?? Array.Empty<Diagnostic>();

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (Diagnostic diagnostic in _diagnostics)
        {
            var item = new ListViewItem(new[]
            {
                diagnostic.Severity.ToString(),
                diagnostic.Line.ToString(),
                diagnostic.Column.ToString(),
                diagnostic.Message,
            })
            {
                Tag = diagnostic,
            };
            _list.Items.Add(item);
        }

        _list.EndUpdate();
        _header.Text = _diagnostics.Count == 0 ? "Problems — none found" : $"Problems — {_diagnostics.Count}";
    }

    public void ApplyTheme(ThemePalette palette)
    {
        BackColor = palette.ChromeBackground;
        _header.BackColor = palette.ChromeBackground;
        _header.ForeColor = palette.ChromeForeground;
        _list.BackColor = palette.IsDark ? ControlPaint.Light(palette.ChromeBackground, 0.05f) : Color.White;
        _list.ForeColor = palette.ChromeForeground;

        _headerBack = palette.IsDark ? ControlPaint.Light(palette.ChromeBackground, 0.12f) : SystemColors.Control;
        _headerFore = palette.ChromeForeground;

        if (_list.IsHandleCreated)
        {
            NativeDarkMode.UseExplorerTheme(_list.Handle, palette.IsDark);
        }

        StretchLastColumn();
        _list.Invalidate();
    }

    /// <summary>Widen the Message column to consume the leftover width, so no unthemed filler shows.</summary>
    private void StretchLastColumn()
    {
        if (_list.Columns.Count < 4)
        {
            return;
        }

        int used = _list.Columns[0].Width + _list.Columns[1].Width + _list.Columns[2].Width;
        int remaining = _list.ClientSize.Width - used;
        _list.Columns[3].Width = Math.Max(200, remaining);
    }

    private void OnDrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using var back = new SolidBrush(_headerBack);
        e.Graphics.FillRectangle(back, e.Bounds);

        using var borderPen = new Pen(ControlPaint.Dark(_headerBack, 0.05f));
        e.Graphics.DrawLine(borderPen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
        e.Graphics.DrawLine(borderPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

        var textBounds = Rectangle.Inflate(e.Bounds, -6, 0);
        TextRenderer.DrawText(
            e.Graphics,
            e.Header?.Text ?? string.Empty,
            _list.Font,
            textBounds,
            _headerFore,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private void OnRowActivated(object? sender, EventArgs e)
    {
        if (_list.SelectedItems.Count > 0 && _list.SelectedItems[0].Tag is Diagnostic diagnostic)
        {
            DiagnosticActivated?.Invoke(this, diagnostic);
        }
    }
}
