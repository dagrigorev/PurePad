using System.Drawing;
using System.Drawing.Drawing2D;

namespace PurePad.View.Controls;

/// <summary>
/// Rasterizes a bundled Material Design Icon (a 24×24 SVG path) into a tinted bitmap for the folder
/// tree. Every icon is drawn from the same icon family and filled with one accent colour, so the
/// tree reads as a single unified set — folders in amber, files coloured by category.
/// </summary>
internal static class FileIconRenderer
{
    public static Bitmap Render(string pathData, Color color, int size)
    {
        var bitmap = new Bitmap(size, size);
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        using GraphicsPath path = SvgPath.Parse(pathData);
        float margin = size * 0.055f;
        float scale = (size - 2 * margin) / 24f;
        using (var transform = new Matrix())
        {
            transform.Translate(margin, margin);
            transform.Scale(scale, scale);
            path.Transform(transform);
        }

        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
        return bitmap;
    }
}
