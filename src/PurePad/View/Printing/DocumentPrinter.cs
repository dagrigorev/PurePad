using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace PurePad.View.Printing;

/// <summary>
/// Prints plain document text with word wrapping, a file-name header and a page-number
/// footer, mirroring Notepad's default print output. Encapsulating printing here keeps
/// the pagination state machine out of the main window (Single Responsibility).
/// </summary>
public sealed class DocumentPrinter
{
    private readonly PrintDocument _printDocument = new();

    private Font _font = SystemFonts.DefaultFont;
    private string _title = string.Empty;
    private Queue<string> _pending = new();
    private int _pageNumber;

    public DocumentPrinter()
    {
        _printDocument.PrintPage += OnPrintPage;
        _printDocument.BeginPrint += (s, e) => _pageNumber = 0;
    }

    /// <summary>Shared page settings so Page Setup and Print agree on margins/orientation.</summary>
    public PageSettings PageSettings
    {
        get => _printDocument.DefaultPageSettings;
        set => _printDocument.DefaultPageSettings = value;
    }

    /// <summary>Show the Page Setup dialog bound to this printer's settings.</summary>
    public void ShowPageSetup(IWin32Window owner)
    {
        using var dialog = new PageSetupDialog { Document = _printDocument };
        dialog.ShowDialog(owner);
    }

    /// <summary>Show the Print dialog and, if confirmed, print <paramref name="text"/>.</summary>
    public void Print(IWin32Window owner, string text, string documentTitle, Font font)
    {
        using var dialog = new PrintDialog
        {
            Document = _printDocument,
            UseEXDialog = true,
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return;
        }

        _font = font;
        _title = documentTitle;
        _pending = new Queue<string>(text.Replace("\r\n", "\n").Split('\n'));
        _printDocument.Print();
    }

    private void OnPrintPage(object? sender, PrintPageEventArgs e)
    {
        Graphics graphics = e.Graphics!;
        _pageNumber++;

        RectangleF bounds = e.MarginBounds;
        float lineHeight = _font.GetHeight(graphics);
        float headerHeight = lineHeight * 1.5f;
        float footerHeight = lineHeight * 1.5f;
        float top = bounds.Top + headerHeight;
        float bottom = bounds.Bottom - footerHeight;

        DrawHeader(graphics, bounds, lineHeight);
        DrawFooter(graphics, bounds, lineHeight);

        float y = top;
        float wrapWidth = bounds.Width;

        while (_pending.Count > 0)
        {
            string line = _pending.Peek();
            foreach (string segment in WrapLine(graphics, line, wrapWidth))
            {
                if (y + lineHeight > bottom)
                {
                    e.HasMorePages = true;
                    return;
                }

                graphics.DrawString(segment, _font, Brushes.Black, bounds.Left, y);
                y += lineHeight;
            }

            _pending.Dequeue();
        }

        e.HasMorePages = false;
    }

    private void DrawHeader(Graphics graphics, RectangleF bounds, float lineHeight)
    {
        graphics.DrawString(_title, _font, Brushes.Black, bounds.Left, bounds.Top);
    }

    private void DrawFooter(Graphics graphics, RectangleF bounds, float lineHeight)
    {
        string footer = $"Page {_pageNumber}";
        SizeF size = graphics.MeasureString(footer, _font);
        float x = bounds.Left + (bounds.Width - size.Width) / 2f;
        graphics.DrawString(footer, _font, Brushes.Black, x, bounds.Bottom - lineHeight);
    }

    /// <summary>Break one logical line into segments that each fit within <paramref name="maxWidth"/>.</summary>
    private IEnumerable<string> WrapLine(Graphics graphics, string line, float maxWidth)
    {
        if (line.Length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        int start = 0;
        while (start < line.Length)
        {
            int count = 1;
            int lastFit = 1;
            while (start + count <= line.Length)
            {
                float width = graphics.MeasureString(line.Substring(start, count), _font).Width;
                if (width > maxWidth)
                {
                    break;
                }

                lastFit = count;
                count++;
            }

            yield return line.Substring(start, lastFit);
            start += lastFit;
        }
    }
}
