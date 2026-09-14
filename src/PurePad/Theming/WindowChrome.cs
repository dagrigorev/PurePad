using System.Drawing;
using System.Runtime.InteropServices;

namespace PurePad.Theming;

/// <summary>
/// Colours a window's non-client area (title bar, border) to match a theme via the Desktop
/// Window Manager. Newer Windows builds honour explicit caption/text/border colours; older
/// ones fall back to the immersive dark-mode flag. All calls are best-effort — on an OS
/// that lacks an attribute the call simply does nothing.
/// </summary>
internal static class WindowChrome
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Apply the theme's chrome colours to the given window handle.</summary>
    public static void Apply(IntPtr handle, ThemePalette palette)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        SetFlag(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, palette.IsDark ? 1 : 0);
        SetColor(handle, DWMWA_CAPTION_COLOR, palette.ChromeBackground);
        SetColor(handle, DWMWA_TEXT_COLOR, palette.ChromeForeground);
        SetColor(handle, DWMWA_BORDER_COLOR, palette.ChromeBorder);
    }

    private static void SetFlag(IntPtr handle, int attribute, int value) => TrySet(handle, attribute, value);

    private static void SetColor(IntPtr handle, int attribute, Color color) =>
        TrySet(handle, attribute, ToColorRef(color));

    private static void TrySet(IntPtr handle, int attribute, int value)
    {
        try
        {
            DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // dwmapi unavailable (non-Windows / very old): ignore.
        }
        catch (EntryPointNotFoundException)
        {
            // Attribute unsupported on this build: ignore.
        }
    }

    /// <summary>Win32 COLORREF is 0x00BBGGRR.</summary>
    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);
}
