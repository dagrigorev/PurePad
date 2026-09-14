using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PurePad.View;

/// <summary>
/// Renders a window to a PNG using <c>PrintWindow</c>, which captures the window's own
/// pixels even when it is behind another window. Used by the <c>--screenshot</c> debug
/// hook so the app can image itself without relying on OS screen-grab of the foreground.
/// </summary>
internal static class ScreenCapture
{
    private const int PW_RENDERFULLCONTENT = 2;

    [DllImport("user32.dll")]
    private static extern int PrintWindow(IntPtr hwnd, IntPtr hdc, int flags);

    public static void Capture(Form form, string path)
    {
        ArgumentNullException.ThrowIfNull(form);

        int w = Math.Max(1, form.Width);
        int h = Math.Max(1, form.Height);

        using var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            IntPtr hdc = g.GetHdc();
            try
            {
                PrintWindow(form.Handle, hdc, PW_RENDERFULLCONTENT);
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
        }

        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        bitmap.Save(path, ImageFormat.Png);
    }
}
